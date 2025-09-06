use crate::domain::entities::UserPreference;
use crate::domain::repositories::UserPreferencesRepository;
use crate::infrastructure::database::PostgresConnection;
use async_trait::async_trait;
use sqlx::Row;
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct PostgresUserPreferencesRepository {
    db: Arc<PostgresConnection>,
}

impl PostgresUserPreferencesRepository {
    pub fn new(db: Arc<PostgresConnection>) -> Self {
        Self { db }
    }
}

#[async_trait]
impl UserPreferencesRepository for PostgresUserPreferencesRepository {
    async fn add_favorite_route(&self, user_id: Uuid, route_name: &str) -> Result<()> {
        sqlx::query(
            r#"
            INSERT INTO user_favorite_routes (user_id, route_name)
            VALUES ($1, $2)
            ON CONFLICT (user_id, route_name) DO NOTHING
            "#,
        )
        .bind(user_id)
        .bind(route_name)
        .execute(&self.db.pool)
        .await?;

        Ok(())
    }

    async fn remove_favorite_route(&self, user_id: Uuid, route_name: &str) -> Result<()> {
        sqlx::query("DELETE FROM user_favorite_routes WHERE user_id = $1 AND route_name = $2")
            .bind(user_id)
            .bind(route_name)
            .execute(&self.db.pool)
            .await?;

        Ok(())
    }

    async fn get_favorite_routes(&self, user_id: Uuid) -> Result<Vec<String>> {
        let rows = sqlx::query(
            "SELECT route_name FROM user_favorite_routes WHERE user_id = $1 ORDER BY added_at ASC",
        )
        .bind(user_id)
        .fetch_all(&self.db.pool)
        .await?;

        let routes = rows
            .into_iter()
            .map(|row| row.get::<String, _>("route_name"))
            .collect();

        Ok(routes)
    }

    async fn set_favorite_routes(&self, user_id: Uuid, routes: &[String]) -> Result<()> {
        let mut transaction = self.db.pool.begin().await?;

        // Clear existing favorites
        sqlx::query("DELETE FROM user_favorite_routes WHERE user_id = $1")
            .bind(user_id)
            .execute(&mut *transaction)
            .await?;

        // Insert new favorites
        for route_name in routes {
            sqlx::query("INSERT INTO user_favorite_routes (user_id, route_name) VALUES ($1, $2)")
                .bind(user_id)
                .bind(route_name)
                .execute(&mut *transaction)
                .await?;
        }

        transaction.commit().await?;
        Ok(())
    }

    async fn set_preference(&self, user_id: Uuid, key: &str, value: &str) -> Result<()> {
        sqlx::query(
            r#"
            INSERT INTO user_preferences (user_id, preference_key, preference_value)
            VALUES ($1, $2, $3)
            ON CONFLICT (user_id, preference_key)
            DO UPDATE SET preference_value = $3, updated_at = NOW()
            "#,
        )
        .bind(user_id)
        .bind(key)
        .bind(value)
        .execute(&self.db.pool)
        .await?;

        Ok(())
    }

    async fn get_preference(&self, user_id: Uuid, key: &str) -> Result<Option<String>> {
        let row = sqlx::query(
            "SELECT preference_value FROM user_preferences WHERE user_id = $1 AND preference_key = $2",
        )
        .bind(user_id)
        .bind(key)
        .fetch_optional(&self.db.pool)
        .await?;

        if let Some(row) = row {
            Ok(Some(row.get("preference_value")))
        } else {
            Ok(None)
        }
    }

    async fn get_all_preferences(&self, user_id: Uuid) -> Result<Vec<UserPreference>> {
        let rows = sqlx::query(
            r#"
            SELECT id, user_id, preference_key, preference_value, created_at, updated_at
            FROM user_preferences WHERE user_id = $1
            "#,
        )
        .bind(user_id)
        .fetch_all(&self.db.pool)
        .await?;

        let preferences = rows
            .into_iter()
            .map(|row| UserPreference {
                id: row.get("id"),
                user_id: row.get("user_id"),
                preference_key: row.get("preference_key"),
                preference_value: row.get("preference_value"),
                created_at: row.get("created_at"),
                updated_at: row.get("updated_at"),
            })
            .collect();

        Ok(preferences)
    }

    async fn delete_preference(&self, user_id: Uuid, key: &str) -> Result<()> {
        sqlx::query("DELETE FROM user_preferences WHERE user_id = $1 AND preference_key = $2")
            .bind(user_id)
            .bind(key)
            .execute(&self.db.pool)
            .await?;

        Ok(())
    }
}
