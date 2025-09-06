use crate::application::AuthUseCases;
use axum::{
    extract::{Request, State},
    http::{HeaderMap, StatusCode},
    middleware::Next,
    response::Response,
};
use std::sync::Arc;
use tower_cookies::Cookies;

#[derive(Debug, Clone)]
pub struct AuthenticatedUser {
    pub user_id: uuid::Uuid,
    pub email: String,
    pub role: String,
}

pub async fn auth_middleware(
    cookies: Cookies,
    headers: HeaderMap,
    State(auth_use_cases): State<Arc<AuthUseCases>>,
    mut request: Request,
    next: Next,
) -> Result<Response, StatusCode> {
    // Try to extract token
    let token = extract_token(&cookies, &headers);

    if let Some(token) = token {
        // Verify the token
        match auth_use_cases.verify_access_token(&token).await {
            Ok(Some(user)) => {
                let auth_user = AuthenticatedUser {
                    user_id: user.id,
                    email: user.email,
                    role: user.role.to_string(),
                };
                request.extensions_mut().insert(auth_user);

                // Continue to the next middleware/handler
                Ok(next.run(request).await)
            }
            Ok(None) => Err(StatusCode::UNAUTHORIZED),
            Err(_) => Err(StatusCode::UNAUTHORIZED),
        }
    } else {
        Err(StatusCode::UNAUTHORIZED)
    }
}

fn extract_token(cookies: &Cookies, headers: &HeaderMap) -> Option<String> {
    // First try Bearer token from Authorization header
    if let Some(auth_header) = headers.get("authorization")
        && let Ok(auth_str) = auth_header.to_str()
        && let Some(stripped) = auth_str.strip_prefix("Bearer ")
    {
        return Some(stripped.to_string());
    }

    // Fallback to cookie
    if let Some(cookie) = cookies.get("access_token") {
        return Some(cookie.value().to_string());
    }

    None
}
