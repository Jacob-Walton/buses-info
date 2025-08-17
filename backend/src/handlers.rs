use crate::{
    cache::BusCache, database::Database, models::BusStatus, scraper::scrape_bus_information,
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
    let bus_values: Vec<BusStatus> = scrape_bus_information().await.unwrap_or_default();

    // Cache the fresh data
    cache.set(bus_values.clone()).await;

    Json(json!({
        "buses": bus_values,
        "cached": false
    }))
}
