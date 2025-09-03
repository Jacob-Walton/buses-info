use crate::domain::entities::User;
use anyhow::{Result, anyhow};
use chrono::{Duration, Utc};
use jsonwebtoken::{DecodingKey, EncodingKey, Header, Validation, decode, encode};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    pub sub: String, // user id
    pub email: String,
    pub role: String,
    pub exp: usize,
    pub iat: usize,
}

impl Claims {
    fn new(user: &User, expires_in_days: i64) -> Self {
        let now = Utc::now();
        let exp = (now + Duration::days(expires_in_days)).timestamp() as usize;
        Self {
            sub: user.id.to_string(),
            email: user.email.clone(),
            role: user.role.to_string(),
            exp,
            iat: now.timestamp() as usize,
        }
    }
}

pub struct JwtService {
    secret: Vec<u8>,
    access_token_duration_days: i64,
}

impl JwtService {
    pub fn new() -> Result<Self> {
        let secret = std::env::var("JWT_SECRET_KEY")
            .map_err(|_| anyhow!("JWT_SECRET_KEY environment variable is not set"))?;

        Ok(Self {
            secret: secret.into_bytes(),
            access_token_duration_days: 7, // Default 7 days
        })
    }

    pub fn generate_access_token(&self, user: &User) -> Result<(String, String)> {
        let claims = Claims::new(user, self.access_token_duration_days);
        let expires_at = Utc::now() + Duration::days(self.access_token_duration_days);
        let expires_at_str = expires_at.to_rfc3339();

        let token = encode(
            &Header::default(),
            &claims,
            &EncodingKey::from_secret(&self.secret),
        )
        .map_err(|e| anyhow!("Failed to generate access token: {}", e))?;

        Ok((token, expires_at_str))
    }

    pub fn generate_refresh_token(&self) -> String {
        Uuid::new_v4().to_string()
    }

    pub fn verify_access_token(&self, token: &str) -> Result<Claims> {
        let token_data = decode::<Claims>(
            token,
            &DecodingKey::from_secret(&self.secret),
            &Validation::default(),
        )
        .map_err(|e| anyhow!("Invalid or expired token: {}", e))?;

        Ok(token_data.claims)
    }

    pub fn extract_user_id_from_token(&self, token: &str) -> Result<Uuid> {
        let claims = self.verify_access_token(token)?;
        Uuid::parse_str(&claims.sub).map_err(|e| anyhow!("Invalid user ID in token: {}", e))
    }
}
