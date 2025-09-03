use crate::application::{BusDto, GetBusRankings, GetCurrentBuses, RankingsQuery};
use crate::infrastructure::{InMemoryCache, RunshawScraper};
use axum::{Json, extract::Query, response::IntoResponse};
use serde_json::json;
use std::sync::Arc;

pub async fn current_bus_information(
    cache: Arc<InMemoryCache>,
    scraper: Arc<RunshawScraper>,
    get_current_buses: Arc<GetCurrentBuses>,
) -> impl IntoResponse {
    // Try cache first
    if let Some(cached_buses) = cache.get().await {
        tracing::debug!("Returning cached bus data");
        let bus_dtos: Vec<BusDto> = cached_buses.into_iter().map(|b| b.into()).collect();

        return Json(json!({
            "buses": bus_dtos,
            "cached": true
        }));
    }

    // Scrape fresh data
    tracing::debug!("Cache miss, scraping fresh bus data");
    #[cfg(debug_assertions)]
    let mut buses = scraper.scrape_buses().await.unwrap_or_default();
    
    #[cfg(not(debug_assertions))]
    let buses = scraper.scrape_buses().await.unwrap_or_default();

    // In debug mode, use dummy data if scraping fails
    #[cfg(debug_assertions)]
    if buses.is_empty() {
        tracing::debug!("Using dummy data in debug mode");
        buses = RunshawScraper::generate_dummy_data();
    }

    // Update cache
    cache.set(buses.clone()).await;

    // Convert to DTOs and use application service
    let bus_dtos: Vec<BusDto> = buses.into_iter().map(|b| b.into()).collect();

    match get_current_buses.execute(bus_dtos.clone()).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Failed to process buses: {}", e);
            Json(json!({
                "buses": bus_dtos,
                "cached": false,
                "error": "Processing error occurred"
            }))
        }
    }
}

pub async fn service_rankings(
    Query(query): Query<RankingsQuery>,
    get_rankings: Arc<GetBusRankings>,
) -> impl IntoResponse {
    match get_rankings.execute(Some(query)).await {
        Ok(response) => Json(json!(response)),
        Err(e) => {
            tracing::error!("Failed to get rankings: {}", e);
            Json(json!({
                "error": "Failed to retrieve rankings"
            }))
        }
    }
}

pub async fn health_check() -> impl IntoResponse {
    "OK"
}

pub async fn health_status(
    db: Arc<crate::infrastructure::PostgresConnection>,
) -> impl IntoResponse {
    let database_connection = db.health_check().await.is_ok();

    Json(json!({
        "database": database_connection,
        "site": true,
        "redis": false
    }))
}
