use sqlx::{PgPool, postgres::PgPoolOptions};
use std::time::Duration;

#[derive(Clone)]
pub struct Database {
    pub pool: PgPool,
}

impl Database {
    pub async fn new(database_url: Option<&str>) -> anyhow::Result<Self> {
        let default_url = "postgresql://buses_user:buses_password@localhost:5432/buses";
        let url = database_url.unwrap_or(default_url);

        let pool = PgPoolOptions::new()
            .max_connections(20)
            .min_connections(5)
            .acquire_timeout(Duration::from_secs(30))
            .connect(url)
            .await?;

        tracing::info!("Database connection established successfully");

        Ok(Database { pool })
    }
}
