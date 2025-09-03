use crate::infrastructure::JwtService;
use axum::{
    Json,
    extract::FromRequestParts,
    http::{StatusCode, request::Parts},
    response::{IntoResponse, Response},
};
use serde_json::json;

pub struct JwtUser {
    pub user_id: String,
    pub email: String,
    pub role: String,
}

impl<S> FromRequestParts<S> for JwtUser
where
    S: Send + Sync,
{
    type Rejection = Response;

    fn from_request_parts(
        parts: &mut Parts,
        _state: &S,
    ) -> impl std::future::Future<
        Output = std::result::Result<Self, <Self as FromRequestParts<S>>::Rejection>,
    > + Send {
        let auth_header = parts
            .headers
            .get("authorization")
            .and_then(|header| header.to_str().ok())
            .map(|s| s.to_string());

        Box::pin(async move {
            let auth_header = auth_header.ok_or_else(|| {
                (
                    StatusCode::UNAUTHORIZED,
                    Json(json!({
                        "error": "Missing authorization header"
                    })),
                )
                    .into_response()
            })?;

            // Check if it starts with "Bearer "
            let token = auth_header.strip_prefix("Bearer ").ok_or_else(|| {
                (
                    StatusCode::UNAUTHORIZED,
                    Json(json!({
                        "error": "Invalid authorization header format"
                    })),
                )
                    .into_response()
            })?;

            if token.is_empty() {
                return Err((
                    StatusCode::UNAUTHORIZED,
                    Json(json!({
                        "error": "Empty token"
                    })),
                )
                    .into_response());
            }

            // Validate JWT token
            let jwt_service = JwtService::new().map_err(|_| {
                (
                    StatusCode::INTERNAL_SERVER_ERROR,
                    Json(json!({
                        "error": "JWT service initialization failed"
                    })),
                )
                    .into_response()
            })?;

            match jwt_service.verify_access_token(token) {
                Ok(claims) => Ok(JwtUser {
                    user_id: claims.sub,
                    email: claims.email,
                    role: claims.role,
                }),
                Err(_) => Err((
                    StatusCode::UNAUTHORIZED,
                    Json(json!({
                        "error": "Invalid JWT token"
                    })),
                )
                    .into_response()),
            }
        })
    }
}
