use axum::Router;
use axum::http::{HeaderValue, Method};
use axum::routing::{get, post, put};
use buses_api::application::{AuthUseCases, GetBusRankings, GetCurrentBuses};
use buses_api::domain::services::{
    BusService, RankingService, UserPreferencesService, UserService,
};
use buses_api::infrastructure::{
    InMemoryCache, JwtService, OAuthService, PasswordService, PostgresBusRepository,
    PostgresConnection, PostgresRankingRepository, PostgresUserPreferencesRepository,
    PostgresUserRepository, RedisService, RunshawScraper,
};
use buses_api::presentation::handlers::auth_handlers::{
    apple_login, google_login, login, logout, me, refresh_token, register,
};
use buses_api::presentation::handlers::user_preferences_handlers::{
    get_favorite_routes, set_favorite_routes,
};
use buses_api::presentation::{
    current_bus_information, health_check, health_status, service_rankings,
};
use std::sync::Arc;
use tokio::net::TcpListener;
use tower_cookies::CookieManagerLayer;
use tower_http::cors::CorsLayer;
use tower_http::trace::TraceLayer;

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
        .with_env_filter(std::env::var("RUST_LOG").unwrap_or("buses_api=debug,warn".to_string()))
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
    let user_preferences_repository =
        Arc::new(PostgresUserPreferencesRepository::new(db_conn.clone()));

    // Domain services
    let bus_service = Arc::new(BusService::new(bus_repository.clone()));
    let ranking_service = Arc::new(RankingService::new(ranking_repository));
    let user_service = Arc::new(UserService::new(user_repository));
    let user_preferences_service =
        Arc::new(UserPreferencesService::new(user_preferences_repository));

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
                move |cookies, json| register(cookies, json, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/login",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |cookies, json| login(cookies, json, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/google",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |cookies, json| google_login(cookies, json, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/apple",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |cookies, json| apple_login(cookies, json, auth_use_cases)
            }),
        )
        .route(
            "/api/auth/refresh",
            post({
                let auth_use_cases = auth_use_cases.clone();
                move |cookies, json| refresh_token(cookies, json, auth_use_cases)
            }),
        )
        .route("/api/auth/logout", post(logout))
        .route(
            "/api/auth/me",
            get({
                let auth_use_cases = auth_use_cases.clone();
                move |cookies| me(cookies, auth_use_cases)
            }),
        )
        .route(
            "/api/users/{user_id}/favorites",
            get({
                let preferences_service = user_preferences_service.clone();
                move |path| get_favorite_routes(path, preferences_service)
            }),
        )
        .route(
            "/api/users/{user_id}/favorites",
            put({
                let preferences_service = user_preferences_service.clone();
                move |path, json| set_favorite_routes(path, json, preferences_service)
            }),
        );

    #[cfg(debug_assertions)]
    let app = app.layer(TraceLayer::new_for_http());

    let app = app
        .layer(
            CorsLayer::new()
                .allow_origin([
                    "http://localhost:3000".parse::<HeaderValue>().unwrap(),
                    "https://accounts.google.com"
                        .parse::<HeaderValue>()
                        .unwrap(),
                ])
                .allow_headers([
                    axum::http::header::CONTENT_TYPE,
                    axum::http::header::AUTHORIZATION,
                    axum::http::header::ACCEPT,
                    axum::http::header::COOKIE,
                    axum::http::header::SET_COOKIE,
                ])
                .allow_methods([
                    Method::GET,
                    Method::POST,
                    Method::PUT,
                    Method::DELETE,
                    Method::OPTIONS,
                ])
                .allow_credentials(true),
        )
        .layer(CookieManagerLayer::new());

    // Start server
    let addr = std::env::var("LISTEN_ADDR").unwrap_or("localhost:4001".to_string());
    let listener = TcpListener::bind(&addr).await?;
    let listen_addr = format!("http://{addr}");

    tracing::info!("Server starting on {}", listen_addr);

    axum::serve(listener, app).await?;

    Ok(())
}
