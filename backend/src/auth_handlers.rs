use axum::{Json, extract::State, http::StatusCode, response::IntoResponse};
use email_address::EmailAddress;
use serde_json::{Value, json};

use crate::{
    auth::{
        AuthResponse, LoginRequest, RegisterRequest, authenticate_user, create_user,
        generate_token, get_user_by_email, hash_password, verify_password,
    },
    cache::BusCache,
    database::Database,
};
use axum::http::HeaderMap;

pub async fn register(
    State((db, _cache)): State<(Database, BusCache)>,
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
    let user = match create_user(&db, &email, &first_name, &last_name, &password_hash).await {
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

    let response = AuthResponse {
        user: user.to_response(),
        token,
    };

    Ok((StatusCode::CREATED, Json(json!(response))))
}

pub async fn login(
    State((db, _cache)): State<(Database, BusCache)>,
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

    let response = AuthResponse {
        user: user.to_response(),
        token,
    };

    Ok((StatusCode::OK, Json(json!(response))))
}

pub async fn me(
    State((db, _cache)): State<(Database, BusCache)>,
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
