use crate::{
    cache::BusCache,
    database::Database,
    models::BusStatus,
    scraper::{generate_dummy_bus_data, scrape_bus_information},
};
use axum::{Json, extract::State, response::IntoResponse};
use serde_json::json;

pub async fn health_check() -> impl IntoResponse {
    "OK"
}

pub async fn health_status(State((db, _cache)): State<(Database, BusCache)>) -> impl IntoResponse {
    let database_connection = sqlx::query("SELECT 1").fetch_one(&db.pool).await.is_ok();
    let site_connection = true;

    Json(json!({
        "database": database_connection,
        "site": site_connection
    }))
}

pub async fn current_bus_information(
    State((_db, cache)): State<(Database, BusCache)>,
) -> impl IntoResponse {
    // Try to get cached data first
    if let Some(cached_buses) = cache.get().await {
        tracing::debug!("Returning cached bus data");
        return Json(json!({
            "buses": cached_buses,
            "cached": true
        }));
    }

    // Cache miss, scrape fresh data
    tracing::debug!("Cache miss, scraping fresh bus data");
    let mut bus_values: Vec<BusStatus> = scrape_bus_information().await.unwrap_or_default();

    // In debug mode, if scraping returned empty results, use dummy data
    #[cfg(debug_assertions)]
    if bus_values.is_empty() {
        tracing::debug!("Scraping returned empty results in debug mode, using dummy data");
        bus_values = generate_dummy_bus_data();
    }

    // In release mode, if scraping failed or returned empty, just return empty

    // Cache the data (whether real or dummy)
    cache.set(bus_values.clone()).await;

    Json(json!({
        "buses": bus_values,
        "cached": false
    }))
}
