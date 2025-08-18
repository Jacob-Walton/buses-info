use axum::routing::{get, post};
use tokio::net::TcpListener;

mod auth;
mod auth_handlers;
mod cache;
mod database;
mod handlers;
mod models;
mod scraper;
mod user_data;

use auth_handlers::*;
use cache::BusCache;
use database::Database;
use handlers::*;
use user_data::*;

#[cfg(test)]
mod tests;

fn default_level() -> tracing::Level {
    if cfg!(debug_assertions) {
        tracing::Level::DEBUG
    } else {
        tracing::Level::INFO
    }
}

#[cfg(debug_assertions)]
async fn create_debug_users(db: &Database) {
    use crate::auth::create_test_users;
    create_test_users(db).await;
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    #[cfg(debug_assertions)]
    {
        println!("Running in debug mode");
        let root_env = std::path::Path::new(".env.local");
        if root_env.exists() {
            dotenvy::from_path(root_env).ok();
        }

        let env_whitelist: Vec<&str> = vec![
            "DATABASE_URL",
            "JWT_SECRET_KEY",
            "LISTEN_ADDR",
            "CACHE_DURATION_MINUTES",
            "RUST_LOG",
        ];

        println!("Environment variables:");
        for var in env_whitelist {
            if let Ok(value) = std::env::var(var) {
                println!("{}: {}", var, value);
            } else {
                eprintln!("Warning: {} is not set", var);
            }
        }
    }

    #[cfg(not(debug_assertions))]
    {
        let root_env = std::path::Path::new("../.env");
        let local_env = std::path::Path::new(".env");
        if root_env.exists() {
            dotenvy::from_path(root_env).ok();
        } else if local_env.exists() {
            dotenvy::from_path(local_env).ok();
        }
    }

    if std::env::var("JWT_SECRET_KEY").is_err() {
        eprintln!(
            "FATAL: JWT_SECRET_KEY environment variable is not set. Please set it in your environment or .env file."
        );
        std::process::exit(1);
    }

    tracing_subscriber::fmt()
        .with_max_level(default_level())
        .with_env_filter("buses_api=debug,warn")
        .init();

    // Initialize database
    let database_url = std::env::var("DATABASE_URL").ok();
    let database = Database::new(database_url.as_deref()).await?;

    #[cfg(debug_assertions)]
    create_debug_users(&database).await;

    // Initialize bus cache
    let cache_duration = std::env::var("CACHE_DURATION_MINUTES")
        .unwrap_or_else(|_| "5".to_string())
        .parse::<u64>()
        .unwrap_or(5);
    let bus_cache = BusCache::new(cache_duration);

    let app_state = (database, bus_cache);

    let app = axum::Router::new()
        .route("/api/health", get(health_check))
        .route("/api/health/status", get(health_status))
        .route("/api/buses/current", get(current_bus_information))
        .route("/api/auth/register", post(register))
        .route("/api/auth/login", post(login))
        .route("/api/auth/logout", post(logout))
        .route("/api/auth/me", get(me))
        .route("/api/user/export-data", post(request_data_export))
        .route("/api/user/data", get(export_user_data))
        .route(
            "/api/user/delete-account",
            axum::routing::delete(delete_user_account),
        )
        .with_state(app_state);

    let addr = std::env::var("LISTEN_ADDR").unwrap_or("localhost:4001".to_string());
    let listener = match TcpListener::bind(&addr).await {
        Ok(l) => l,
        Err(e) => {
            eprintln!("Failed to bind to {addr}: {e:?}");
            std::process::exit(1);
        }
    };

    let listen_addr = format!("http://{addr}");
    println!("Listening on {listen_addr}");

    tracing::info!("Listening on address: {}", listen_addr);

    if let Err(e) = axum::serve(listener, app).await {
        eprintln!("Server error: {e:?}");
        std::process::exit(1);
    }

    Ok(())
}
