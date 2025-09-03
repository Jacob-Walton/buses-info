use crate::application::dtos::{
    AppleTokenRequest, AuthResponse, GoogleTokenRequest, LoginRequest, RefreshTokenRequest,
    RegisterRequest, UserDto,
};
use crate::domain::services::UserService;
use crate::infrastructure::{JwtService, OAuthService, PasswordService, RedisService};
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[derive(Debug, thiserror::Error)]
pub enum AuthError {
    #[error("Invalid credentials")]
    InvalidCredentials,
    #[error("User not found")]
    UserNotFound,
    #[error("Token generation failed")]
    TokenError,
}

pub struct AuthUseCases {
    user_service: Arc<UserService>,
    oauth_service: Arc<OAuthService>,
    redis_service: Arc<RedisService>,
    jwt_service: Arc<JwtService>,
    password_service: Arc<PasswordService>,
}

impl AuthUseCases {
    pub fn new(
        user_service: Arc<UserService>,
        oauth_service: Arc<OAuthService>,
        redis_service: Arc<RedisService>,
        jwt_service: Arc<JwtService>,
        password_service: Arc<PasswordService>,
    ) -> Self {
        Self {
            user_service,
            oauth_service,
            redis_service,
            jwt_service,
            password_service,
        }
    }

    pub async fn register(&self, request: RegisterRequest) -> Result<AuthResponse> {
        // Hash password
        let password_hash = self
            .password_service
            .hash_password(&request.password)
            .map_err(|e| format!("Failed to hash password: {e}"))?;

        // Create user
        let user = self
            .user_service
            .create_user(
                request.email.clone(),
                request.first_name,
                request.last_name,
                password_hash,
            )
            .await?;

        // Generate tokens
        let (access_token, expires_at) = self.jwt_service.generate_access_token(&user)?;
        let refresh_token = self.jwt_service.generate_refresh_token();

        // Store refresh token in Redis (30 days expiration)
        self.redis_service
            .store_refresh_token(user.id, &refresh_token, 30 * 24 * 60 * 60)
            .await?;

        Ok(AuthResponse {
            user: user.into(),
            token: access_token,
            refresh_token: Some(refresh_token),
            expires_at,
            expires_in: Some(7 * 24 * 60 * 60), // 7 days in seconds
        })
    }

    pub async fn login(&self, request: LoginRequest) -> Result<AuthResponse> {
        // Authenticate
        if let Some(user) = self
            .user_service
            .authenticate(&request.email, &request.password)
            .await?
        {
            let (access_token, expires_at) = self.jwt_service.generate_access_token(&user)?;
            let refresh_token = self.jwt_service.generate_refresh_token();

            // Store refresh token in Redis (30 days expiration)
            self.redis_service
                .store_refresh_token(user.id, &refresh_token, 30 * 24 * 60 * 60)
                .await?;

            Ok(AuthResponse {
                user: user.into(),
                token: access_token,
                refresh_token: Some(refresh_token),
                expires_at,
                expires_in: Some(7 * 24 * 60 * 60), // 7 days in seconds
            })
        } else {
            Err(Box::new(AuthError::InvalidCredentials))
        }
    }

    pub async fn get_user_by_id(&self, user_id: Uuid) -> Result<Option<UserDto>> {
        if let Some(user) = self.user_service.find_by_id(user_id).await? {
            Ok(Some(user.into()))
        } else {
            Ok(None)
        }
    }

    pub async fn verify_token(
        &self,
        token: &str,
        verify_jwt: impl Fn(&str) -> Result<Uuid>,
    ) -> Result<Option<UserDto>> {
        match verify_jwt(token) {
            Ok(user_id) => self.get_user_by_id(user_id).await,
            Err(_) => Ok(None),
        }
    }

    pub async fn google_login(&self, request: GoogleTokenRequest) -> Result<AuthResponse> {
        // Verify Google token
        let claims = self
            .oauth_service
            .verify_google_token(&request.id_token)
            .await
            .map_err(|e| format!("Google token verification failed: {e}"))?;

        // Find or create user
        let user = self
            .user_service
            .find_or_create_social_user(&claims.email, &claims.given_name, &claims.family_name)
            .await?;

        // Generate tokens
        let (access_token, expires_at) = self.jwt_service.generate_access_token(&user)?;
        let refresh_token = self.jwt_service.generate_refresh_token();

        // Store refresh token in Redis (30 days expiration)
        self.redis_service
            .store_refresh_token(user.id, &refresh_token, 30 * 24 * 60 * 60)
            .await?;

        Ok(AuthResponse {
            user: user.into(),
            token: access_token,
            refresh_token: Some(refresh_token),
            expires_at,
            expires_in: Some(7 * 24 * 60 * 60), // 7 days in seconds
        })
    }

    pub async fn apple_login(&self, request: AppleTokenRequest) -> Result<AuthResponse> {
        // Verify Apple token
        let claims = self
            .oauth_service
            .verify_apple_token(&request.id_token)
            .await
            .map_err(|e| format!("Apple token verification failed: {e}"))?;

        // Apple doesn't provide name fields in the token, so use email username as name
        let username = claims.email.split('@').next().unwrap_or("User");

        // Find or create user
        let user = self
            .user_service
            .find_or_create_social_user(&claims.email, username, "")
            .await?;

        // Generate tokens using JWT service
        let (access_token, expires_at) = self.jwt_service.generate_access_token(&user)?;
        let refresh_token = self.jwt_service.generate_refresh_token();

        // Store refresh token in Redis (30 days expiration)
        self.redis_service
            .store_refresh_token(user.id, &refresh_token, 30 * 24 * 60 * 60)
            .await?;

        Ok(AuthResponse {
            user: user.into(),
            token: access_token,
            refresh_token: Some(refresh_token),
            expires_at,
            expires_in: Some(7 * 24 * 60 * 60), // 7 days in seconds
        })
    }

    pub async fn refresh_token(&self, request: RefreshTokenRequest) -> Result<AuthResponse> {
        // Validate refresh token and get user ID from Redis
        if let Some(user_id) = self
            .redis_service
            .validate_refresh_token(&request.refresh_token)
            .await?
        {
            // Get user from database
            if let Some(user) = self.user_service.get_user_by_id(user_id).await? {
                // Invalidate old refresh token
                self.redis_service
                    .invalidate_refresh_token(&request.refresh_token)
                    .await?;

                // Generate new tokens using JWT service
                let (access_token, expires_at) = self.jwt_service.generate_access_token(&user)?;
                let new_refresh_token = self.jwt_service.generate_refresh_token();

                // Store new refresh token in Redis (30 days expiration)
                self.redis_service
                    .store_refresh_token(user.id, &new_refresh_token, 30 * 24 * 60 * 60)
                    .await?;

                Ok(AuthResponse {
                    user: user.into(),
                    token: access_token,
                    refresh_token: Some(new_refresh_token),
                    expires_at,
                    expires_in: Some(7 * 24 * 60 * 60), // 7 days in seconds
                })
            } else {
                Err("User not found".into())
            }
        } else {
            Err("Invalid refresh token".into())
        }
    }

    pub async fn verify_access_token(&self, token: &str) -> Result<Option<UserDto>> {
        match self.jwt_service.extract_user_id_from_token(token) {
            Ok(user_id) => self.get_user_by_id(user_id).await,
            Err(_) => Ok(None),
        }
    }
}
