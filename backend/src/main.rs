use axum::Router;
use axum::routing::{get, post};
use buses_api::application::{AuthUseCases, GetBusRankings, GetCurrentBuses};
use buses_api::domain::services::{BusService, RankingService, UserService};
use buses_api::infrastructure::{
    InMemoryCache, JwtService, OAuthService, PasswordService, PostgresBusRepository,
    PostgresConnection, PostgresRankingRepository, PostgresUserRepository, RedisService,
    RunshawScraper,
};
use buses_api::presentation::{
    apple_login, current_bus_information, google_login, health_check, health_status, login, logout,
    me, refresh_token, register, service_rankings,
};
use std::sync::Arc;
use tokio::net::TcpListener;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // Initialize environment
    #[cfg(debug_assertions)]
    {
        println!("Running in debug mode");
        let root_env = std::path::Path::new(".env.local");
        if root_env.exists() {
            dotenvy::from_path(root_env).ok();
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

    // Initialize tracing
    tracing_subscriber::fmt()
        .with_max_level(tracing::Level::DEBUG)
        .with_env_filter("buses_api=debug,warn")
        .init();

    // Initialize infrastructure
    tracing::info!("Initializing infrastructure...");

    // Database
    let database_url = std::env::var("DATABASE_URL").ok();
    tracing::info!("Creating database connection...");
    let db_conn = Arc::new(PostgresConnection::new(database_url.as_deref()).await?);
    tracing::info!("Running database migrations...");
    db_conn.migrate().await?;
    tracing::info!("Database setup complete");

    // Repositories
    let bus_repository = Arc::new(PostgresBusRepository::new(db_conn.clone()));
    let ranking_repository = Arc::new(PostgresRankingRepository::new(db_conn.clone()));
    let user_repository = Arc::new(PostgresUserRepository::new(db_conn.clone()));

    // Domain services
    let bus_service = Arc::new(BusService::new(bus_repository.clone()));
    let ranking_service = Arc::new(RankingService::new(ranking_repository));
    let user_service = Arc::new(UserService::new(user_repository));

    // Infrastructure services
    let oauth_service = Arc::new(OAuthService::new());

    // Redis service for refresh tokens
    let redis_url = std::env::var("REDIS_URL").ok();
    let redis_service = Arc::new(
        RedisService::new(redis_url.as_deref())
            .await
            .map_err(|e| anyhow::anyhow!("Failed to create Redis service: {}", e))?,
    );

    // JWT service for token management
    let jwt_service = Arc::new(
        JwtService::new().map_err(|e| anyhow::anyhow!("Failed to create JWT service: {}", e))?,
    );

    // Password service for hashing
    let password_service = Arc::new(PasswordService::new());

    let cache_duration = std::env::var("CACHE_DURATION_MINUTES")
        .unwrap_or_else(|_| "5".to_string())
        .parse::<u64>()
        .unwrap_or(5);
    let cache = Arc::new(InMemoryCache::new(cache_duration));
    let scraper = Arc::new(
        RunshawScraper::new().map_err(|e| anyhow::anyhow!("Failed to create scraper: {}", e))?,
    );

    // Application services
    let get_current_buses = Arc::new(GetCurrentBuses::new(bus_service));
    let get_rankings = Arc::new(GetBusRankings::new(ranking_service));
    let auth_use_cases = Arc::new(AuthUseCases::new(
        user_service.clone(),
        oauth_service,
        redis_service,
        jwt_service,
        password_service,
    ));

    // Create test users in debug mode
    #[cfg(debug_assertions)]
    {
        if let Err(e) = user_service.create_test_users().await {
            tracing::warn!("Failed to create test users: {}", e);
        } else {
            tracing::info!("Test users created successfully");
        }
    }

    tracing::info!("All services initialized successfully");

    // Build router with dependency injection
    let app = Router::new()
        .route("/api/health", get(health_check))
        .route(
            "/api/health/status",
            get({
                let db = db_conn.clone();
                move || health_status(db)
            }),
        )
        .route(
            "/api/buses/current",
            get({
                let cache = cache.clone();
                let scraper = scraper.clone();
                let get_current_buses = get_current_buses.clone();
                move || current_bus_information(cache, scraper, get_current_buses)
            }),
        )
        .route(
            "/api/buses/rankings",
            get({
                let get_rankings = get_rankings.clone();
                move |query| service_rankings(query, get_rankings)
            }),
        )
        .route(
            "/api/auth/register",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |request| register(request, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/login",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |request| login(request, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/google",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |request| google_login(request, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/apple",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |request| apple_login(request, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/refresh",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |request| refresh_token(request, auth_use_cases)
            }),
        )
        .route("/api/auth/logout", post(logout))
        .route("/api/auth/me", get(me));

    // Start server
    let addr = std::env::var("LISTEN_ADDR").unwrap_or("localhost:4001".to_string());
    let listener = TcpListener::bind(&addr).await?;
    let listen_addr = format!("http://{addr}");

    tracing::info!("Server starting on {}", listen_addr);

    axum::serve(listener, app).await?;

    Ok(())
}
