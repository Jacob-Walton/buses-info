//! Test module

pub mod auth_tests;
pub mod cache_tests;
pub mod database_tests;
pub mod handlers_tests;
pub mod integration_tests;
pub mod scraper_tests;
pub mod user_data_tests;

use crate::{cache::BusCache, database::Database};
use std::sync::Once;

static INIT: Once = Once::new();

pub fn init_test_env() {
    INIT.call_once(|| {
        unsafe { std::env::set_var("JWT_SECRET_KEY", "test_secret_key_for_testing_only") };

        // Use environment variable if set (for CI), otherwise use default
        if std::env::var("DATABASE_URL").is_err() {
            unsafe {
                std::env::set_var(
                    "DATABASE_URL",
                    "postgresql://test_user:test_pass@localhost:5433/test_buses",
                )
            };
        }
    });
}

pub async fn setup_test_database() -> Database {
    init_test_env();

    // Get DATABASE_URL from environment
    let database_url = std::env::var("DATABASE_URL").unwrap_or_else(|_| {
        "postgresql://test_user:test_pass@localhost:5433/test_buses".to_string()
    });

    // Try to connect to the test database
    let mut attempts = 0;
    let max_attempts = 10;

    loop {
        match Database::new(Some(&database_url)).await {
            Ok(db) => {
                // Clean up tables for tests
                let _ = sqlx::query("TRUNCATE TABLE users CASCADE")
                    .execute(&db.pool)
                    .await;
                return db;
            }
            Err(e) => {
                attempts += 1;
                if attempts >= max_attempts {
                    panic!(
                        "Failed to connect to test database after {max_attempts} attempts. Database URL: {database_url}\nError: {e}"
                    );
                }
                tokio::time::sleep(std::time::Duration::from_millis(1000)).await;
            }
        }
    }
}

pub fn setup_test_cache() -> BusCache {
    BusCache::new(1) // 1 minute TTL for tests
}
