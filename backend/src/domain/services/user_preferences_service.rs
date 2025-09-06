use crate::domain::entities::{FavoriteRoutesResponse, UserPreference};
use crate::domain::repositories::UserPreferencesRepository;
use chrono::Utc;
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[derive(Debug, thiserror::Error)]
pub enum UserPreferencesError {
    #[error("Route name cannot be empty")]
    EmptyRouteName,
    #[error("Route name too long (max 255 characters)")]
    RouteNameTooLong,
    #[error("Too many favorite routes (max 50)")]
    TooManyFavorites,
    #[error("Preference key cannot be empty")]
    EmptyPreferenceKey,
    #[error("Preference value too long (max 1000 characters)")]
    PreferenceValueTooLong,
}

pub struct UserPreferencesService {
    repository: Arc<dyn UserPreferencesRepository>,
}

impl UserPreferencesService {
    pub fn new(repository: Arc<dyn UserPreferencesRepository>) -> Self {
        Self { repository }
    }

    pub async fn add_favorite_route(&self, user_id: Uuid, route_name: String) -> Result<()> {
        self.validate_route_name(&route_name)?;

        // Check current count
        let current_routes = self.repository.get_favorite_routes(user_id).await?;
        if current_routes.len() >= 50 {
            return Err(Box::new(UserPreferencesError::TooManyFavorites));
        }

        self.repository
            .add_favorite_route(user_id, &route_name)
            .await
    }

    pub async fn remove_favorite_route(&self, user_id: Uuid, route_name: String) -> Result<()> {
        self.validate_route_name(&route_name)?;
        self.repository
            .remove_favorite_route(user_id, &route_name)
            .await
    }

    pub async fn get_favorite_routes(&self, user_id: Uuid) -> Result<FavoriteRoutesResponse> {
        let routes = self.repository.get_favorite_routes(user_id).await?;

        Ok(FavoriteRoutesResponse {
            routes,
            last_updated: Utc::now(),
        })
    }

    pub async fn set_favorite_routes(&self, user_id: Uuid, routes: Vec<String>) -> Result<()> {
        // Validate all routes
        for route in &routes {
            self.validate_route_name(route)?;
        }

        if routes.len() > 50 {
            return Err(Box::new(UserPreferencesError::TooManyFavorites));
        }

        // Remove duplicates
        let mut unique_routes = Vec::new();
        for route in routes {
            if !unique_routes.contains(&route) {
                unique_routes.push(route);
            }
        }

        self.repository
            .set_favorite_routes(user_id, &unique_routes)
            .await
    }

    pub async fn toggle_favorite_route(&self, user_id: Uuid, route_name: String) -> Result<bool> {
        self.validate_route_name(&route_name)?;

        let current_routes = self.repository.get_favorite_routes(user_id).await?;

        if current_routes.contains(&route_name) {
            self.repository
                .remove_favorite_route(user_id, &route_name)
                .await?;
            Ok(false) // Removed
        } else {
            if current_routes.len() >= 50 {
                return Err(Box::new(UserPreferencesError::TooManyFavorites));
            }
            self.repository
                .add_favorite_route(user_id, &route_name)
                .await?;
            Ok(true) // Added
        }
    }

    pub async fn set_preference(&self, user_id: Uuid, key: String, value: String) -> Result<()> {
        self.validate_preference_key(&key)?;
        self.validate_preference_value(&value)?;

        self.repository.set_preference(user_id, &key, &value).await
    }

    pub async fn get_preference(&self, user_id: Uuid, key: String) -> Result<Option<String>> {
        self.validate_preference_key(&key)?;
        self.repository.get_preference(user_id, &key).await
    }

    pub async fn get_all_preferences(&self, user_id: Uuid) -> Result<Vec<UserPreference>> {
        self.repository.get_all_preferences(user_id).await
    }

    pub async fn delete_preference(&self, user_id: Uuid, key: String) -> Result<()> {
        self.validate_preference_key(&key)?;
        self.repository.delete_preference(user_id, &key).await
    }

    fn validate_route_name(&self, route_name: &str) -> Result<()> {
        if route_name.trim().is_empty() {
            return Err(Box::new(UserPreferencesError::EmptyRouteName));
        }

        if route_name.len() > 255 {
            return Err(Box::new(UserPreferencesError::RouteNameTooLong));
        }

        Ok(())
    }

    fn validate_preference_key(&self, key: &str) -> Result<()> {
        if key.trim().is_empty() {
            return Err(Box::new(UserPreferencesError::EmptyPreferenceKey));
        }

        Ok(())
    }

    fn validate_preference_value(&self, value: &str) -> Result<()> {
        if value.len() > 1000 {
            return Err(Box::new(UserPreferencesError::PreferenceValueTooLong));
        }

        Ok(())
    }
}
