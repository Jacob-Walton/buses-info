use anyhow::Result;
use redis::{AsyncCommands, Client};
use serde::{Deserialize, Serialize};
use tracing::debug;
use uuid::Uuid;

#[derive(Debug, Clone)]
pub struct RedisService {
    client: Client,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct RefreshTokenData {
    pub user_id: String,
    pub created_at: chrono::DateTime<chrono::Utc>,
}

impl RedisService {
    pub fn new(redis_url: Option<&str>) -> Result<Self> {
        let url = redis_url.unwrap_or("redis://127.0.0.1:6379");
        let client = Client::open(url)?;
        Ok(Self { client })
    }

    pub async fn store_refresh_token(
        &self,
        user_id: &str,
        refresh_token: &str,
        ttl_days: u64,
    ) -> Result<()> {
        let mut conn = self.client.get_multiplexed_tokio_connection().await?;

        let token_data = RefreshTokenData {
            user_id: user_id.to_string(),
            created_at: chrono::Utc::now(),
        };

        let serialized = serde_json::to_string(&token_data)?;
        let ttl_seconds = ttl_days * 24 * 60 * 60;

        let _: () = conn
            .set_ex(
                format!("refresh_token:{refresh_token}"),
                serialized,
                ttl_seconds,
            )
            .await?;

        debug!("Stored refresh token for user {}", user_id);
        Ok(())
    }

    pub async fn get_refresh_token_data(
        &self,
        refresh_token: &str,
    ) -> Result<Option<RefreshTokenData>> {
        let mut conn = self.client.get_multiplexed_tokio_connection().await?;

        let key = format!("refresh_token:{refresh_token}");
        let data: Option<String> = conn.get(&key).await?;

        match data {
            Some(serialized) => {
                let token_data: RefreshTokenData = serde_json::from_str(&serialized)?;
                Ok(Some(token_data))
            }
            None => Ok(None),
        }
    }

    pub async fn invalidate_refresh_token(&self, refresh_token: &str) -> Result<()> {
        let mut conn = self.client.get_multiplexed_tokio_connection().await?;

        let key = format!("refresh_token:{refresh_token}");
        let _: () = conn.del(&key).await?;

        debug!("Invalidated refresh token");
        Ok(())
    }

    pub async fn invalidate_all_user_tokens(&self, user_id: &str) -> Result<()> {
        let mut conn = self.client.get_multiplexed_tokio_connection().await?;

        // Find all refresh tokens for this user
        let pattern = "refresh_token:*";
        let keys: Vec<String> = conn.keys(pattern).await?;

        for key in keys {
            if let Ok(Some(data)) = self
                .get_refresh_token_data(&key.replace("refresh_token:", ""))
                .await
                && data.user_id == user_id
            {
                let _: () = conn.del(&key).await?;
            }
        }

        debug!("Invalidated all refresh tokens for user {}", user_id);
        Ok(())
    }

    pub fn generate_refresh_token() -> String {
        Uuid::new_v4().to_string()
    }

    pub async fn health_check(&self) -> Result<()> {
        let mut conn = self.client.get_multiplexed_tokio_connection().await?;
        let _: String = conn.ping().await?;
        Ok(())
    }
}
