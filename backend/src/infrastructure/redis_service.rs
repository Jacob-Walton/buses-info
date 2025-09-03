use anyhow::{Result, anyhow};
use redis::{AsyncCommands, Client};
use std::sync::Arc;
use uuid::Uuid;

pub struct RedisService {
    client: Arc<Client>,
}

impl RedisService {
    pub async fn new(redis_url: Option<&str>) -> Result<Self> {
        let url = redis_url.unwrap_or("redis://localhost:6379");
        let client =
            Client::open(url).map_err(|e| anyhow!("Failed to create Redis client: {}", e))?;

        // Test connection
        let mut conn = client
            .get_multiplexed_async_connection()
            .await
            .map_err(|e| anyhow!("Failed to connect to Redis: {}", e))?;

        let _: String = conn
            .ping()
            .await
            .map_err(|e| anyhow!("Redis ping failed: {}", e))?;

        tracing::info!("Connected to Redis successfully");

        Ok(Self {
            client: Arc::new(client),
        })
    }

    pub async fn store_refresh_token(
        &self,
        user_id: Uuid,
        refresh_token: &str,
        expires_in_seconds: u64,
    ) -> Result<()> {
        let mut conn = self.client.get_multiplexed_async_connection().await?;
        let key = format!("refresh_token:{refresh_token}");
        let user_key = format!("user_refresh:{user_id}");

        // Store refresh token with expiration (default 30 days)
        let _: () = conn
            .set_ex(&key, user_id.to_string(), expires_in_seconds)
            .await?;

        // Also store by user ID
        let _: () = conn
            .set_ex(&user_key, refresh_token, expires_in_seconds)
            .await?;

        tracing::debug!("Stored refresh token for user {}", user_id);
        Ok(())
    }

    pub async fn validate_refresh_token(&self, refresh_token: &str) -> Result<Option<Uuid>> {
        let mut conn = self.client.get_multiplexed_async_connection().await?;
        let key = format!("refresh_token:{refresh_token}");

        let user_id_str: Option<String> = conn.get(&key).await?;

        if let Some(user_id_str) = user_id_str {
            let user_id = Uuid::parse_str(&user_id_str)
                .map_err(|e| anyhow!("Invalid user ID in Redis: {}", e))?;
            Ok(Some(user_id))
        } else {
            Ok(None)
        }
    }

    pub async fn invalidate_refresh_token(&self, refresh_token: &str) -> Result<()> {
        let mut conn = self.client.get_multiplexed_async_connection().await?;
        let key = format!("refresh_token:{refresh_token}");

        // Get user ID before deleting to also clean up user_refresh key
        let user_id_result: Result<Option<String>, _> = conn.get(&key).await;
        if let Ok(Some(user_id_str)) = user_id_result
            && let Ok(user_id) = Uuid::parse_str(&user_id_str)
        {
            let user_key = format!("user_refresh:{user_id}");
            let _: () = conn.del(&user_key).await?;
        }

        let _: () = conn.del(&key).await?;
        tracing::debug!("Invalidated refresh token");
        Ok(())
    }

    pub async fn invalidate_all_user_tokens(&self, user_id: Uuid) -> Result<()> {
        let mut conn = self.client.get_multiplexed_async_connection().await?;
        let user_key = format!("user_refresh:{user_id}");

        // Get the current refresh token for this user
        let refresh_token_result: Result<Option<String>, _> = conn.get(&user_key).await;
        if let Ok(Some(refresh_token)) = refresh_token_result {
            let token_key = format!("refresh_token:{refresh_token}");
            let _: () = conn.del(&token_key).await?;
        }

        let _: () = conn.del(&user_key).await?;
        tracing::debug!("Invalidated all tokens for user {}", user_id);
        Ok(())
    }

    pub async fn health_check(&self) -> Result<()> {
        let mut conn = self.client.get_multiplexed_async_connection().await?;
        let _: String = conn.ping().await?;
        Ok(())
    }
}
