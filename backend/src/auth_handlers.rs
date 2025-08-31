use axum::{Json, extract::State, http::StatusCode, response::IntoResponse};
use email_address::EmailAddress;
use serde_json::{Value, json};

use crate::{
    auth::{
        AppleTokenRequest, AuthResponse, GoogleTokenRequest, LoginRequest, RefreshTokenRequest,
        RegisterRequest, authenticate_user, create_user, find_or_create_social_user,
        generate_token, get_user_by_email, get_user_by_id, hash_password, verify_apple_token,
        verify_google_token, verify_password,
    },
    cache::BusCache,
    database::Database,
    redis_service::RedisService,
};
use axum::http::HeaderMap;

pub async fn register(
    State((db, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    Json(req): Json<RegisterRequest>,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Validate input
    if req.email.is_empty()
        || req.password.is_empty()
        || req.first_name.is_empty()
        || req.last_name.is_empty()
    {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "All fields are required"})),
        ));
    }

    // Email validation
    if !EmailAddress::is_valid(req.email.trim()) {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Please enter a valid email address"})),
        ));
    }

    // Password validation
    if req.password.len() < 8 {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Password must be at least 8 characters long"})),
        ));
    }

    // Name validation
    if req.first_name.trim().is_empty() || req.last_name.trim().is_empty() {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "First name and last name cannot be empty"})),
        ));
    }

    // Normalize data
    let email = req.email.trim().to_lowercase();
    let first_name = req.first_name.trim().to_string();
    let last_name = req.last_name.trim().to_string();

    // Check if user already exists
    match get_user_by_email(&db, &email).await {
        Ok(Some(_)) => {
            return Err((
                StatusCode::CONFLICT,
                Json(json!({"error": "User already exists"})),
            ));
        }
        Ok(None) => {
            // User doesn't exist, continue
        }
        Err(e) => {
            tracing::error!("Database error checking existing user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Database error"})),
            ));
        }
    }

    // Hash password
    let password_hash = match hash_password(&req.password) {
        Ok(hash) => hash,
        Err(e) => {
            tracing::error!("Password hashing error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to process password"})),
            ));
        }
    };

    // Create user
    let user = match create_user(
        &db,
        &email,
        &first_name,
        &last_name,
        &password_hash,
        req.terms_accepted,
    )
    .await
    {
        Ok(user) => user,
        Err(e) => {
            tracing::error!("Failed to create user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to create user"})),
            ));
        }
    };

    // Generate token
    let token = match generate_token(&user) {
        Ok(token) => token,
        Err(e) => {
            tracing::error!("Token generation error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to generate token"})),
            ));
        }
    };

    // Generate refresh token if Redis is available
    let refresh_token = if let Some(redis_service) = &redis {
        let refresh_token = crate::redis_service::RedisService::generate_refresh_token();
        if let Err(e) = redis_service
            .store_refresh_token(&user.id, &refresh_token, 30)
            .await
        {
            tracing::error!("Failed to store refresh token: {}", e);
            None
        } else {
            Some(refresh_token)
        }
    } else {
        None
    };

    // Calculate expiration (7 days from now)
    let expires_at = (chrono::Utc::now() + chrono::Duration::days(7)).to_rfc3339();

    let response = AuthResponse {
        user: user.to_response(),
        token,
        refresh_token,
        expires_at,
    };

    Ok((StatusCode::CREATED, Json(json!(response))))
}

pub async fn login(
    State((db, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    Json(req): Json<LoginRequest>,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Validate input
    if req.email.is_empty() || req.password.is_empty() {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Email and password are required"})),
        ));
    }

    // Normalize email
    let email = req.email.trim().to_lowercase();

    // Find user
    let user = match get_user_by_email(&db, &email).await {
        Ok(Some(user)) => user,
        Ok(None) => {
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Invalid credentials"})),
            ));
        }
        Err(e) => {
            tracing::error!("Database error finding user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Database error"})),
            ));
        }
    };

    // Verify password
    let password_valid = match verify_password(&req.password, &user.password_hash) {
        Ok(valid) => valid,
        Err(e) => {
            tracing::error!("Password verification error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Authentication error"})),
            ));
        }
    };

    if !password_valid {
        return Err((
            StatusCode::UNAUTHORIZED,
            Json(json!({"error": "Invalid credentials"})),
        ));
    }

    // Generate token
    let token = match generate_token(&user) {
        Ok(token) => token,
        Err(e) => {
            tracing::error!("Token generation error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to generate token"})),
            ));
        }
    };

    // Generate refresh token if Redis is available
    let refresh_token = if let Some(redis_service) = &redis {
        let refresh_token = crate::redis_service::RedisService::generate_refresh_token();
        if let Err(e) = redis_service
            .store_refresh_token(&user.id, &refresh_token, 30)
            .await
        {
            tracing::error!("Failed to store refresh token: {}", e);
            None
        } else {
            Some(refresh_token)
        }
    } else {
        None
    };

    let expires_at = (chrono::Utc::now() + chrono::Duration::days(7)).to_rfc3339();

    let response = AuthResponse {
        user: user.to_response(),
        token,
        refresh_token,
        expires_at,
    };

    Ok((StatusCode::OK, Json(json!(response))))
}

pub async fn me(
    State((db, _cache, _redis)): State<(Database, BusCache, Option<RedisService>)>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Extract authorization header
    let auth_header = headers
        .get("authorization")
        .and_then(|header| header.to_str().ok())
        .and_then(|header| header.strip_prefix("Bearer "));

    let token = match auth_header {
        Some(token) => token,
        None => {
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Missing authorization header"})),
            ));
        }
    };

    // Authenticate user
    let user = match authenticate_user(&db, token).await {
        Ok(user) => user,
        Err(_) => {
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Invalid token"})),
            ));
        }
    };

    Ok(Json(json!(user.to_response())))
}

pub async fn logout() -> impl IntoResponse {
    // In this system, logout is usually handled client-side
    // by removing the token. Here we just return success.
    StatusCode::OK
}

pub async fn google_login(
    State((db, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    Json(req): Json<GoogleTokenRequest>,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Validate input
    if req.id_token.is_empty() {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Google ID token is required"})),
        ));
    }

    // Verify Google token
    let claims = match verify_google_token(&req.id_token).await {
        Ok(claims) => claims,
        Err(e) => {
            tracing::error!("Google token verification error: {}", e);
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Invalid Google token"})),
            ));
        }
    };

    // Check if email is verified
    if !claims.email_verified {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Email not verified with Google"})),
        ));
    }

    // Find or create user
    let user = match find_or_create_social_user(
        &db,
        &claims.email,
        &claims.given_name,
        &claims.family_name,
        "google",
    )
    .await
    {
        Ok(user) => user,
        Err(e) => {
            tracing::error!("Failed to find/create social user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to process user account"})),
            ));
        }
    };

    // Generate token
    let token = match generate_token(&user) {
        Ok(token) => token,
        Err(e) => {
            tracing::error!("Token generation error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to generate token"})),
            ));
        }
    };

    // Generate refresh token if Redis is available
    let refresh_token = if let Some(redis_service) = &redis {
        let refresh_token = crate::redis_service::RedisService::generate_refresh_token();
        if let Err(e) = redis_service
            .store_refresh_token(&user.id, &refresh_token, 30)
            .await
        {
            tracing::error!("Failed to store refresh token: {}", e);
            None
        } else {
            Some(refresh_token)
        }
    } else {
        None
    };

    let expires_at = (chrono::Utc::now() + chrono::Duration::days(7)).to_rfc3339();

    let response = AuthResponse {
        user: user.to_response(),
        token,
        refresh_token,
        expires_at,
    };

    Ok((StatusCode::OK, Json(json!(response))))
}

pub async fn apple_login(
    State((db, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    Json(req): Json<AppleTokenRequest>,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Validate input
    if req.id_token.is_empty() {
        return Err((
            StatusCode::BAD_REQUEST,
            Json(json!({"error": "Apple ID token is required"})),
        ));
    }

    // Verify Apple token
    let claims = match verify_apple_token(&req.id_token).await {
        Ok(claims) => claims,
        Err(e) => {
            tracing::error!("Apple token verification error: {}", e);
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Invalid Apple token"})),
            ));
        }
    };

    // Extract name parts from email if not provided separately by Apple
    let email_parts: Vec<&str> = claims.email.split('@').collect();
    let default_username = "user";
    let username = email_parts.first().unwrap_or(&default_username);

    // For Apple, we usually don't get separate first/last names
    // so we'll use the email username as both
    // FIXME: Prompt the user later on to set their actual name
    let first_name = username.to_string();
    let last_name = "".to_string();

    // Find or create user
    let user = match find_or_create_social_user(
        &db,
        &claims.email,
        &first_name,
        &last_name,
        "apple",
    )
    .await
    {
        Ok(user) => user,
        Err(e) => {
            tracing::error!("Failed to find/create social user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to process user account"})),
            ));
        }
    };

    // Generate token
    let token = match generate_token(&user) {
        Ok(token) => token,
        Err(e) => {
            tracing::error!("Token generation error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to generate token"})),
            ));
        }
    };

    // Generate refresh token if Redis is available
    let refresh_token = if let Some(redis_service) = &redis {
        let refresh_token = crate::redis_service::RedisService::generate_refresh_token();
        if let Err(e) = redis_service
            .store_refresh_token(&user.id, &refresh_token, 30)
            .await
        {
            tracing::error!("Failed to store refresh token: {}", e);
            None
        } else {
            Some(refresh_token)
        }
    } else {
        None
    };

    let expires_at = (chrono::Utc::now() + chrono::Duration::days(7)).to_rfc3339();

    let response = AuthResponse {
        user: user.to_response(),
        token,
        refresh_token,
        expires_at,
    };

    Ok((StatusCode::OK, Json(json!(response))))
}

pub async fn validate_token(
    State((db, _cache, _redis)): State<(Database, BusCache, Option<RedisService>)>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Extract authorization header
    let auth_header = headers
        .get("authorization")
        .and_then(|header| header.to_str().ok())
        .and_then(|header| header.strip_prefix("Bearer "));

    let token = match auth_header {
        Some(token) => token,
        None => {
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Missing authorization header"})),
            ));
        }
    };

    // Validate token
    match authenticate_user(&db, token).await {
        Ok(user) => Ok(Json(json!({
            "isValid": true,
            "user": user.to_response()
        }))),
        Err(_) => Ok(Json(json!({
            "isValid": false
        }))),
    }
}

pub async fn refresh_token(
    State((db, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    Json(req): Json<RefreshTokenRequest>,
) -> Result<impl IntoResponse, (StatusCode, Json<Value>)> {
    // Check if Redis is available
    let redis_service = match &redis {
        Some(service) => service,
        None => {
            return Err((
                StatusCode::SERVICE_UNAVAILABLE,
                Json(json!({"error": "Refresh token service unavailable"})),
            ));
        }
    };

    // Validate refresh token
    let token_data = match redis_service
        .get_refresh_token_data(&req.refresh_token)
        .await
    {
        Ok(Some(data)) => data,
        Ok(None) => {
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "Invalid or expired refresh token"})),
            ));
        }
        Err(e) => {
            tracing::error!("Redis error validating refresh token: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Token validation error"})),
            ));
        }
    };

    // Get user from database
    let user = match get_user_by_id(&db, &token_data.user_id).await {
        Ok(Some(user)) => user,
        Ok(None) => {
            // User no longer exists, invalidate the refresh token
            let _ = redis_service
                .invalidate_refresh_token(&req.refresh_token)
                .await;
            return Err((
                StatusCode::UNAUTHORIZED,
                Json(json!({"error": "User not found"})),
            ));
        }
        Err(e) => {
            tracing::error!("Database error finding user: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Database error"})),
            ));
        }
    };

    // Generate new access token
    let new_token = match generate_token(&user) {
        Ok(token) => token,
        Err(e) => {
            tracing::error!("Token generation error: {}", e);
            return Err((
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({"error": "Failed to generate token"})),
            ));
        }
    };

    // Generate new refresh token
    let new_refresh_token = crate::redis_service::RedisService::generate_refresh_token();

    // Store new refresh token and invalidate old one
    if let Err(e) = redis_service
        .store_refresh_token(&user.id, &new_refresh_token, 30)
        .await
    {
        tracing::error!("Failed to store new refresh token: {}", e);
        return Err((
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(json!({"error": "Failed to generate refresh token"})),
        ));
    }

    // Invalidate old refresh token
    let _ = redis_service
        .invalidate_refresh_token(&req.refresh_token)
        .await;

    let expires_at = (chrono::Utc::now() + chrono::Duration::days(7)).to_rfc3339();

    let response = AuthResponse {
        user: user.to_response(),
        token: new_token,
        refresh_token: Some(new_refresh_token),
        expires_at,
    };

    Ok((StatusCode::OK, Json(json!(response))))
}
