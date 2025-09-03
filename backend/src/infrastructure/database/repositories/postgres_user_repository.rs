use crate::domain::entities::{User, UserCredentials};
use crate::domain::repositories::UserRepository;
use crate::infrastructure::database::PostgresConnection;
use async_trait::async_trait;
use chrono::Utc;
use sqlx::Row;
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct PostgresUserRepository {
    db: Arc<PostgresConnection>,
}

impl PostgresUserRepository {
    pub fn new(db: Arc<PostgresConnection>) -> Self {
        Self { db }
    }
}

#[async_trait]
impl UserRepository for PostgresUserRepository {
    async fn create_user(&self, user: &User, credentials: &UserCredentials) -> Result<()> {
        let mut transaction = self.db.pool.begin().await?;

        sqlx::query(
            r#"
            INSERT INTO users (id, email, first_name, last_name, password_hash, role, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            "#
        )
        .bind(user.id)
        .bind(&user.email)
        .bind(&user.first_name)
        .bind(&user.last_name)
        .bind(&credentials.password_hash)
        .bind(user.role.to_string())
        .bind(user.created_at)
        .bind(user.updated_at)
        .execute(&mut *transaction)
        .await?;

        transaction.commit().await?;
        Ok(())
    }

    async fn find_by_id(&self, id: Uuid) -> Result<Option<User>> {
        let row = sqlx::query(
            r#"
            SELECT id, email, first_name, last_name, role, created_at, updated_at
            FROM users WHERE id = $1
            "#,
        )
        .bind(id)
        .fetch_optional(&self.db.pool)
        .await?;

        if let Some(row) = row {
            let user = User {
                id: row.get("id"),
                email: row.get("email"),
                first_name: row.get("first_name"),
                last_name: row.get("last_name"),
                role: row.get::<String, _>("role").parse().unwrap_or_default(),
                created_at: row.get("created_at"),
                updated_at: row.get("updated_at"),
            };
            Ok(Some(user))
        } else {
            Ok(None)
        }
    }

    async fn find_by_email(&self, email: &str) -> Result<Option<User>> {
        let row = sqlx::query(
            r#"
            SELECT id, email, first_name, last_name, role, created_at, updated_at
            FROM users WHERE email = $1
            "#,
        )
        .bind(email)
        .fetch_optional(&self.db.pool)
        .await?;

        if let Some(row) = row {
            let user = User {
                id: row.get("id"),
                email: row.get("email"),
                first_name: row.get("first_name"),
                last_name: row.get("last_name"),
                role: row.get::<String, _>("role").parse().unwrap_or_default(),
                created_at: row.get("created_at"),
                updated_at: row.get("updated_at"),
            };
            Ok(Some(user))
        } else {
            Ok(None)
        }
    }

    async fn get_credentials(&self, email: &str) -> Result<Option<UserCredentials>> {
        let row = sqlx::query("SELECT email, password_hash FROM users WHERE email = $1")
            .bind(email)
            .fetch_optional(&self.db.pool)
            .await?;

        if let Some(row) = row {
            let credentials = UserCredentials {
                email: row.get("email"),
                password_hash: row.get("password_hash"),
            };
            Ok(Some(credentials))
        } else {
            Ok(None)
        }
    }

    async fn update_user(&self, user: &User) -> Result<()> {
        sqlx::query(
            r#"
            UPDATE users 
            SET email = $2, first_name = $3, last_name = $4, role = $5, 
                updated_at = $6
            WHERE id = $1
            "#,
        )
        .bind(user.id)
        .bind(&user.email)
        .bind(&user.first_name)
        .bind(&user.last_name)
        .bind(user.role.to_string())
        .bind(user.updated_at)
        .execute(&self.db.pool)
        .await?;

        Ok(())
    }

    async fn delete_user(&self, id: Uuid) -> Result<()> {
        sqlx::query("DELETE FROM users WHERE id = $1")
            .bind(id)
            .execute(&self.db.pool)
            .await?;

        Ok(())
    }

    async fn verify_email(&self, id: Uuid) -> Result<()> {
        sqlx::query("UPDATE users SET email_verified = true, updated_at = $2 WHERE id = $1")
            .bind(id)
            .bind(Utc::now())
            .execute(&self.db.pool)
            .await?;

        Ok(())
    }
}
