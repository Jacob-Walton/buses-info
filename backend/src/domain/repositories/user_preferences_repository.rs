use crate::domain::entities::UserPreference;
use async_trait::async_trait;
use std::error::Error;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[async_trait]
pub trait UserPreferencesRepository: Send + Sync {
    // Favorite routes management
    async fn add_favorite_route(&self, user_id: Uuid, route_name: &str) -> Result<()>;
    async fn remove_favorite_route(&self, user_id: Uuid, route_name: &str) -> Result<()>;
    async fn get_favorite_routes(&self, user_id: Uuid) -> Result<Vec<String>>;
    async fn set_favorite_routes(&self, user_id: Uuid, routes: &[String]) -> Result<()>;

    // General preferences management
    async fn set_preference(&self, user_id: Uuid, key: &str, value: &str) -> Result<()>;
    async fn get_preference(&self, user_id: Uuid, key: &str) -> Result<Option<String>>;
    async fn get_all_preferences(&self, user_id: Uuid) -> Result<Vec<UserPreference>>;
    async fn delete_preference(&self, user_id: Uuid, key: &str) -> Result<()>;
}
