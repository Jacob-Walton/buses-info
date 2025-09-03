use crate::domain::entities::{User, UserCredentials};
use async_trait::async_trait;
use std::error::Error;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[async_trait]
pub trait UserRepository: Send + Sync {
    async fn create_user(&self, user: &User, credentials: &UserCredentials) -> Result<()>;
    async fn find_by_id(&self, id: Uuid) -> Result<Option<User>>;
    async fn find_by_email(&self, email: &str) -> Result<Option<User>>;
    async fn get_credentials(&self, email: &str) -> Result<Option<UserCredentials>>;
    async fn update_user(&self, user: &User) -> Result<()>;
    async fn delete_user(&self, id: Uuid) -> Result<()>;
    async fn verify_email(&self, id: Uuid) -> Result<()>;
}
