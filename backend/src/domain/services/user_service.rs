use crate::domain::entities::{User, UserCredentials, UserRole};
use crate::domain::repositories::UserRepository;
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[derive(Debug, thiserror::Error)]
pub enum UserServiceError {
    #[error("User with email {0} already exists")]
    EmailAlreadyExists(String),
    #[error("User not found")]
    UserNotFound,
    #[error("Invalid email format")]
    InvalidEmail,
    #[error("Password too weak")]
    WeakPassword,
}

pub struct UserService {
    repository: Arc<dyn UserRepository>,
}

impl UserService {
    pub fn new(repository: Arc<dyn UserRepository>) -> Self {
        Self { repository }
    }

    pub async fn create_user(
        &self,
        email: String,
        first_name: String,
        last_name: String,
        password_hash: String,
    ) -> Result<User> {
        // Validate email format
        if !self.is_valid_email(&email) {
            return Err(Box::new(UserServiceError::InvalidEmail));
        }

        // Check if user already exists
        if self.repository.find_by_email(&email).await?.is_some() {
            return Err(Box::new(UserServiceError::EmailAlreadyExists(email)));
        }

        let user = User::new(email.clone(), first_name, last_name);
        let credentials = UserCredentials {
            email: email.clone(),
            password_hash,
        };

        self.repository.create_user(&user, &credentials).await?;
        Ok(user)
    }

    pub async fn find_by_id(&self, id: Uuid) -> Result<Option<User>> {
        self.repository.find_by_id(id).await
    }

    pub async fn find_by_email(&self, email: &str) -> Result<Option<User>> {
        self.repository.find_by_email(email).await
    }

    pub async fn authenticate(&self, email: &str, password_hash: &str) -> Result<Option<User>> {
        if let Some(credentials) = self.repository.get_credentials(email).await?
            && credentials.password_hash == password_hash
        {
            return self.repository.find_by_email(email).await;
        }
        Ok(None)
    }

    pub async fn update_user(&self, user: &User) -> Result<()> {
        // Validate email format if it changed
        if !self.is_valid_email(&user.email) {
            return Err(Box::new(UserServiceError::InvalidEmail));
        }

        self.repository.update_user(user).await
    }

    pub async fn delete_user(&self, id: Uuid) -> Result<()> {
        // Check if user exists
        if self.repository.find_by_id(id).await?.is_none() {
            return Err(Box::new(UserServiceError::UserNotFound));
        }

        self.repository.delete_user(id).await
    }

    pub async fn verify_email(&self, id: Uuid) -> Result<()> {
        self.repository.verify_email(id).await
    }

    pub async fn promote_to_admin(&self, user_id: Uuid) -> Result<()> {
        if let Some(mut user) = self.repository.find_by_id(user_id).await? {
            user.role = UserRole::Admin;
            user.updated_at = chrono::Utc::now();
            self.repository.update_user(&user).await?;
            Ok(())
        } else {
            Err(Box::new(UserServiceError::UserNotFound))
        }
    }

    fn is_valid_email(&self, email: &str) -> bool {
        email_address::EmailAddress::is_valid(email)
    }

    pub fn validate_user_data(&self, user: &User) -> Vec<String> {
        let mut errors = Vec::new();

        if user.first_name.trim().is_empty() {
            errors.push("First name cannot be empty".to_string());
        }

        if user.last_name.trim().is_empty() {
            errors.push("Last name cannot be empty".to_string());
        }

        if !self.is_valid_email(&user.email) {
            errors.push("Invalid email format".to_string());
        }

        errors
    }

    pub async fn create_test_users(&self) -> Result<()> {
        let test_users = vec![
            ("test@example.com", "Test", "User", "password"),
            ("admin@example.com", "Admin", "User", "admin"),
        ];

        for (email, first_name, last_name, password) in test_users {
            // Check if user already exists
            if self.repository.find_by_email(email).await?.is_some() {
                tracing::debug!("Test user {} already exists, skipping", email);
                continue;
            }

            // Hash password
            let password_hash = bcrypt::hash(password, bcrypt::DEFAULT_COST)
                .map_err(|e| format!("Failed to hash password: {e}"))?;

            let user = User::new(
                email.to_string(),
                first_name.to_string(),
                last_name.to_string(),
            );
            let credentials = UserCredentials {
                email: email.to_string(),
                password_hash,
            };

            self.repository.create_user(&user, &credentials).await?;
            tracing::debug!("Created test user: {}", email);
        }

        Ok(())
    }

    pub async fn find_or_create_social_user(
        &self,
        email: &str,
        first_name: &str,
        last_name: &str,
    ) -> Result<User> {
        // First, try to find existing user by email
        if let Some(existing_user) = self.repository.find_by_email(email).await? {
            return Ok(existing_user);
        }

        // If user doesn't exist, create a new one
        let dummy_password = uuid::Uuid::new_v4().to_string();
        let password_hash = bcrypt::hash(dummy_password, bcrypt::DEFAULT_COST)
            .map_err(|e| format!("Failed to hash password: {e}"))?;

        let user = User::new(
            email.to_string(),
            first_name.to_string(),
            last_name.to_string(),
        );
        let credentials = UserCredentials {
            email: email.to_string(),
            password_hash,
        };

        self.repository.create_user(&user, &credentials).await?;

        // Return the created user
        self.repository
            .find_by_email(email)
            .await?
            .ok_or_else(|| "Failed to retrieve created user".into())
    }

    pub async fn get_user_by_id(&self, id: Uuid) -> Result<Option<User>> {
        self.repository.find_by_id(id).await
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_is_valid_email() {
        let service = UserService::new(Arc::new(MockUserRepository));

        assert!(service.is_valid_email("test@example.com"));
        assert!(service.is_valid_email("user.name+tag@example.com"));

        assert!(!service.is_valid_email("invalid"));
        assert!(!service.is_valid_email("@example.com"));
        assert!(!service.is_valid_email("test@"));
    }

    #[test]
    fn test_validate_user_data() {
        let service = UserService::new(Arc::new(MockUserRepository));

        let valid_user = User::new(
            "test@example.com".to_string(),
            "John".to_string(),
            "Doe".to_string(),
        );

        let errors = service.validate_user_data(&valid_user);
        assert!(errors.is_empty());

        let invalid_user = User::new("invalid-email".to_string(), "".to_string(), "".to_string());

        let errors = service.validate_user_data(&invalid_user);
        assert_eq!(errors.len(), 3);
    }

    // Mock repository for testing
    struct MockUserRepository;

    #[async_trait::async_trait]
    impl UserRepository for MockUserRepository {
        async fn create_user(&self, _user: &User, _credentials: &UserCredentials) -> Result<()> {
            Ok(())
        }
        async fn find_by_id(&self, _id: Uuid) -> Result<Option<User>> {
            Ok(None)
        }
        async fn find_by_email(&self, _email: &str) -> Result<Option<User>> {
            Ok(None)
        }
        async fn get_credentials(&self, _email: &str) -> Result<Option<UserCredentials>> {
            Ok(None)
        }
        async fn update_user(&self, _user: &User) -> Result<()> {
            Ok(())
        }
        async fn delete_user(&self, _id: Uuid) -> Result<()> {
            Ok(())
        }
        async fn verify_email(&self, _id: Uuid) -> Result<()> {
            Ok(())
        }
    }
}
