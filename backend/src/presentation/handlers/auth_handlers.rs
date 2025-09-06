use crate::application::{
    AppleTokenRequest, AuthUseCases, GoogleTokenRequest, LoginRequest, RefreshTokenRequest,
    RegisterRequest, TokenResponseFormat,
};
use axum::{Json, response::IntoResponse};
use serde_json::json;
use std::sync::Arc;
use tower_cookies::{Cookie, Cookies};

pub async fn register(
    cookies: Cookies,
    Json(request): Json<RegisterRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.register(request.clone()).await {
        Ok(response) => {
            match request.format {
                TokenResponseFormat::Cookie => {
                    // Set access token cookie
                    let access_cookie = Cookie::build(("access_token", response.token.clone()))
                        .http_only(true)
                        .secure(cfg!(not(debug_assertions)))
                        .same_site(tower_cookies::cookie::SameSite::Strict)
                        .max_age(time::Duration::days(7))
                        .build();

                    cookies.add(access_cookie);

                    // Set refresh token cookie if present
                    if let Some(ref refresh_token) = response.refresh_token {
                        let refresh_cookie =
                            Cookie::build(("refresh_token", refresh_token.clone()))
                                .http_only(true)
                                .secure(cfg!(not(debug_assertions)))
                                .same_site(tower_cookies::cookie::SameSite::Strict)
                                .max_age(time::Duration::days(30))
                                .build();
                        cookies.add(refresh_cookie);
                    }

                    Json(json!({
                        "user": response.user,
                        "expiresAt": response.expires_at,
                        "expiresIn": response.expires_in
                    }))
                }
                TokenResponseFormat::Json => Json(json!({
                    "user": response.user,
                    "expiresAt": response.expires_at,
                    "expiresIn": response.expires_in,
                    "accessToken": response.token,
                    "refreshToken": response.refresh_token,
                })),
            }
        }
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
    cookies: Cookies,
    Json(request): Json<LoginRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.login(request.clone()).await {
        Ok(response) => {
            match request.format {
                TokenResponseFormat::Cookie => {
                    // Set access token cookie
                    let access_cookie = Cookie::build(("access_token", response.token.clone()))
                        .http_only(true)
                        .secure(cfg!(not(debug_assertions)))
                        .same_site(tower_cookies::cookie::SameSite::Strict)
                        .max_age(time::Duration::days(7))
                        .path("/")
                        .build();
                    cookies.add(access_cookie);

                    // Set refresh token cookie if present
                    if let Some(ref refresh_token) = response.refresh_token {
                        let refresh_cookie =
                            Cookie::build(("refresh_token", refresh_token.clone()))
                                .http_only(true)
                                .secure(cfg!(not(debug_assertions)))
                                .same_site(tower_cookies::cookie::SameSite::Strict)
                                .max_age(time::Duration::days(30))
                                .path("/")
                                .build();
                        cookies.add(refresh_cookie);
                    }

                    Json(json!({
                        "user": response.user,
                        "expiresAt": response.expires_at,
                        "expiresIn": response.expires_in
                    }))
                }
                TokenResponseFormat::Json => Json(json!({
                    "user": response.user,
                    "expiresAt": response.expires_at,
                    "expiresIn": response.expires_in,
                    "accessToken": response.token,
                    "refreshToken": response.refresh_token,
                })),
            }
        }
        Err(e) => {
            tracing::error!("Login failed: {}", e);
            Json(json!({
                "error": "Invalid credentials"
            }))
        }
    }
}

pub async fn logout(cookies: Cookies) -> impl IntoResponse {
    // Clear both cookies by setting them to expire immediately
    let access_cookie = Cookie::build(("access_token", ""))
        .http_only(true)
        .secure(cfg!(not(debug_assertions)))
        .same_site(tower_cookies::cookie::SameSite::Strict)
        .max_age(time::Duration::ZERO)
        .path("/")
        .build();

    let refresh_cookie = Cookie::build(("refresh_token", ""))
        .http_only(true)
        .secure(cfg!(not(debug_assertions)))
        .same_site(tower_cookies::cookie::SameSite::Strict)
        .max_age(time::Duration::ZERO)
        .path("/")
        .build();

    cookies.add(access_cookie);
    cookies.add(refresh_cookie);

    Json(json!({
        "message": "Logged out successfully"
    }))
}

pub async fn me(cookies: Cookies, auth_use_cases: Arc<AuthUseCases>) -> impl IntoResponse {
    if let Some(access_cookie) = cookies.get("access_token") {
        let token = access_cookie.value();

        match auth_use_cases.verify_access_token(token).await {
            Ok(Some(user)) => Json(json!({
                "user": user
            })),
            Ok(None) => Json(json!({
                "error": "Invalid token"
            })),
            Err(_) => Json(json!({
                "error": "Token verification failed"
            })),
        }
    } else {
        Json(json!({
            "error": "No access token"
        }))
    }
}

pub async fn google_login(
    cookies: Cookies,
    Json(request): Json<GoogleTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.google_login(request.clone()).await {
        Ok(response) => {
            match request.format {
                TokenResponseFormat::Cookie => {
                    // Set access token cookie
                    let access_cookie = Cookie::build(("access_token", response.token.clone()))
                        .http_only(true)
                        .secure(cfg!(not(debug_assertions)))
                        .same_site(tower_cookies::cookie::SameSite::Strict)
                        .max_age(time::Duration::days(7))
                        .path("/")
                        .build();
                    cookies.add(access_cookie);

                    // Set refresh token cookie if present
                    if let Some(ref refresh_token) = response.refresh_token {
                        let refresh_cookie =
                            Cookie::build(("refresh_token", refresh_token.clone()))
                                .http_only(true)
                                .secure(cfg!(not(debug_assertions)))
                                .same_site(tower_cookies::cookie::SameSite::Strict)
                                .max_age(time::Duration::days(30))
                                .path("/")
                                .build();
                        cookies.add(refresh_cookie);
                    }

                    Json(json!({
                        "user": response.user,
                        "expiresAt": response.expires_at,
                        "expiresIn": response.expires_in
                    }))
                }
                TokenResponseFormat::Json => Json(json!({
                    "user": response.user,
                    "expiresAt": response.expires_at,
                    "expiresIn": response.expires_in,
                    "accessToken": response.token,
                    "refreshToken": response.refresh_token,
                })),
            }
        }
        Err(e) => {
            tracing::error!("Google login failed: {}", e);
            Json(json!({
                "error": "Google authentication failed"
            }))
        }
    }
}

pub async fn apple_login(
    cookies: Cookies,
    Json(request): Json<AppleTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    match auth_use_cases.apple_login(request.clone()).await {
        Ok(response) => {
            match request.format {
                TokenResponseFormat::Cookie => {
                    // Set access token cookie
                    let access_cookie = Cookie::build(("access_token", response.token.clone()))
                        .http_only(true)
                        .secure(cfg!(not(debug_assertions)))
                        .same_site(tower_cookies::cookie::SameSite::Strict)
                        .max_age(time::Duration::days(7))
                        .path("/")
                        .build();
                    cookies.add(access_cookie);

                    // Set refresh token cookie if present
                    if let Some(ref refresh_token) = response.refresh_token {
                        let refresh_cookie =
                            Cookie::build(("refresh_token", refresh_token.clone()))
                                .http_only(true)
                                .secure(cfg!(not(debug_assertions)))
                                .same_site(tower_cookies::cookie::SameSite::Strict)
                                .max_age(time::Duration::days(30))
                                .path("/")
                                .build();
                        cookies.add(refresh_cookie);
                    }

                    Json(json!({
                        "user": response.user,
                        "expiresAt": response.expires_at,
                        "expiresIn": response.expires_in
                    }))
                }
                TokenResponseFormat::Json => Json(json!({
                    "user": response.user,
                    "expiresAt": response.expires_at,
                    "expiresIn": response.expires_in,
                    "accessToken": response.token,
                    "refreshToken": response.refresh_token,
                })),
            }
        }
        Err(e) => {
            tracing::error!("Apple login failed: {}", e);
            Json(json!({
                "error": "Apple authentication failed"
            }))
        }
    }
}

pub async fn refresh_token(
    cookies: Cookies,
    Json(request): Json<RefreshTokenRequest>,
    auth_use_cases: Arc<AuthUseCases>,
) -> impl IntoResponse {
    // Try to get refresh token from request body, fallback to cookie
    let refresh_token = if !request.refresh_token.is_empty() {
        request.refresh_token.clone()
    } else if let Some(refresh_cookie) = cookies.get("refresh_token") {
        refresh_cookie.value().to_string()
    } else {
        return Json(json!({
            "error": "No refresh token provided"
        }));
    };

    let refresh_request = RefreshTokenRequest {
        refresh_token,
        format: request.format.clone(),
    };

    match auth_use_cases.refresh_token(refresh_request).await {
        Ok(response) => {
            match request.format {
                TokenResponseFormat::Cookie => {
                    // Set new access token cookie
                    let access_cookie = Cookie::build(("access_token", response.token.clone()))
                        .http_only(true)
                        .secure(cfg!(not(debug_assertions)))
                        .same_site(tower_cookies::cookie::SameSite::Strict)
                        .max_age(time::Duration::days(7))
                        .path("/")
                        .build();
                    cookies.add(access_cookie);

                    // Set new refresh token cookie if provided
                    if let Some(ref new_refresh_token) = response.refresh_token {
                        let refresh_cookie =
                            Cookie::build(("refresh_token", new_refresh_token.clone()))
                                .http_only(true)
                                .secure(cfg!(not(debug_assertions)))
                                .same_site(tower_cookies::cookie::SameSite::Strict)
                                .max_age(time::Duration::days(30))
                                .path("/")
                                .build();
                        cookies.add(refresh_cookie);
                    }

                    Json(json!({
                        "user": response.user,
                        "expiresAt": response.expires_at,
                        "expiresIn": response.expires_in
                    }))
                }
                TokenResponseFormat::Json => Json(json!({
                    "user": response.user,
                    "expiresAt": response.expires_at,
                    "expiresIn": response.expires_in,
                    "accessToken": response.token,
                    "refreshToken": response.refresh_token,
                })),
            }
        }
        Err(e) => {
            tracing::error!("Token refresh failed: {}", e);
            Json(json!({
                "error": "Token refresh failed"
            }))
        }
    }
}
