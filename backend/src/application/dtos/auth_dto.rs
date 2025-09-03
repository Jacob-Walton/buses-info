use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Deserialize)]
pub struct RegisterRequest {
    pub email: String,
    pub first_name: String,
    pub last_name: String,
    pub password: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct LoginRequest {
    pub email: String,
    pub password: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct GoogleTokenRequest {
    #[serde(rename = "idToken")]
    pub id_token: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct AppleTokenRequest {
    #[serde(rename = "idToken")]
    pub id_token: String,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RefreshTokenRequest {
    #[serde(rename = "refreshToken")]
    pub refresh_token: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct AuthResponse {
    pub user: UserDto,
    #[serde(rename = "accessToken")]
    pub token: String,
    #[serde(rename = "refreshToken")]
    pub refresh_token: Option<String>,
    #[serde(rename = "expiresAt")]
    pub expires_at: String,
    #[serde(rename = "expiresIn")]
    pub expires_in: Option<i32>,
}

#[derive(Debug, Clone, Serialize)]
pub struct UserDto {
    pub id: Uuid,
    pub email: String,
    pub first_name: String,
    pub last_name: String,
    pub role: String,
}

impl From<crate::domain::entities::User> for UserDto {
    fn from(user: crate::domain::entities::User) -> Self {
        Self {
            id: user.id,
            email: user.email,
            first_name: user.first_name,
            last_name: user.last_name,
            role: user.role.to_string(),
        }
    }
}
