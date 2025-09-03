use crate::application::{
    AppleTokenRequest, AuthUseCases, GoogleTokenRequest, LoginRequest, RefreshTokenRequest,
    RegisterRequest,
};
use crate::presentation::middleware::JwtUser;
use axum::{Json, response::IntoResponse};
use serde_json::json;
use std::sync::Arc;

pub async fn register(
    Json(request): Json<RegisterRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.register(request).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Registration failed: {}", e);
            Json(json!({
                "error": "Registration failed",
                "message": e.to_string()
            }))
        }
    }
}

pub async fn login(
    Json(request): Json<LoginRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.login(request).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Login failed: {}", e);
            Json(json!({
                "error": "Invalid credentials"
            }))
        }
    }
}

pub async fn logout() -> impl IntoResponse {
    // TODO: Invalidate tokens
    Json(json!({
        "message": "Logged out successfully"
    }))
}

pub async fn me(jwt_user: JwtUser) -> impl IntoResponse {
    Json(json!({
        "user": {
            "id": jwt_user.user_id,
            "email": jwt_user.email,
            "role": jwt_user.role
        }
    }))
}

pub async fn google_login(
    Json(request): Json<GoogleTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.google_login(request).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Google login failed: {}", e);
            Json(json!({
                "error": "Google authentication failed"
            }))
        }
    }
}

pub async fn apple_login(
    Json(request): Json<AppleTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.apple_login(request).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Apple login failed: {}", e);
            Json(json!({
                "error": "Apple authentication failed"
            }))
        }
    }
}

pub async fn refresh_token(
    Json(request): Json<RefreshTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.refresh_token(request).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Token refresh failed: {}", e);
            Json(json!({
                "error": "Token refresh failed"
            }))
        }
    }
}
