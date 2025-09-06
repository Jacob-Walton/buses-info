use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct UserPreference {
    pub id: Uuid,
    pub user_id: Uuid,
    pub preference_key: String,
    pub preference_value: String,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct UserFavoriteRoute {
    pub id: Uuid,
    pub user_id: Uuid,
    pub route_name: String,
    pub added_at: DateTime<Utc>,
}

impl UserFavoriteRoute {
    pub fn new(user_id: Uuid, route_name: String) -> Self {
        Self {
            id: Uuid::new_v4(),
            user_id,
            route_name,
            added_at: Utc::now(),
        }
    }
}

impl UserPreference {
    pub fn new(user_id: Uuid, preference_key: String, preference_value: String) -> Self {
        let now = Utc::now();
        Self {
            id: Uuid::new_v4(),
            user_id,
            preference_key,
            preference_value,
            created_at: now,
            updated_at: now,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FavoriteRoutesResponse {
    pub routes: Vec<String>,
    pub last_updated: DateTime<Utc>,
}
