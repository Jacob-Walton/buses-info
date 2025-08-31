use crate::handlers::{current_bus_information, health_check, health_status};
use crate::models::BusStatus;
use crate::tests::{setup_test_cache, setup_test_database};
use axum::extract::State;
use axum::response::IntoResponse;
use serde_json::Value;

#[tokio::test]
async fn test_health_check() {
    let response = health_check().await;
    let response = response.into_response();

    // Should return 200 OK
    assert_eq!(response.status(), 200);
}

#[tokio::test]
async fn test_health_status() {
    let db = setup_test_database().await;
    let cache = setup_test_cache();
    let state = State((db, cache, None));

    let response = health_status(state).await;
    let response = response.into_response();

    // Should return 200 OK
    assert_eq!(response.status(), 200);

    // Extract the body and parse as JSON
    let body = axum::body::to_bytes(response.into_body(), usize::MAX)
        .await
        .unwrap();
    let json: Value = serde_json::from_slice(&body).unwrap();

    // Should have database and site status
    assert!(json.get("database").is_some());
    assert!(json.get("site").is_some());

    // Database should be true (connected)
    assert_eq!(json["database"], true);
    assert_eq!(json["site"], true);
}

#[tokio::test]
async fn test_current_bus_information_cache_miss() {
    let db = setup_test_database().await;
    let cache = setup_test_cache();
    let state = State((db, cache, None));

    let response = current_bus_information(state).await;
    let response = response.into_response();

    // Should return 200 OK regardless of scraper success/failure
    assert_eq!(response.status(), 200);

    // Extract the body and parse as JSON
    let body = axum::body::to_bytes(response.into_body(), usize::MAX)
        .await
        .unwrap();
    let json: Value = serde_json::from_slice(&body).unwrap();

    // Should have buses array and cached flag
    assert!(json.get("buses").is_some());
    assert!(json.get("cached").is_some());

    // Should indicate not cached (fresh data)
    assert_eq!(json["cached"], false);

    assert!(json["buses"].is_array());
}

#[tokio::test]
async fn test_current_bus_information_cache_hit() {
    let db = setup_test_database().await;
    let cache = setup_test_cache();

    // Pre-populate cache with test data
    let test_data = vec![
        BusStatus {
            service: "Test Service 1".to_string(),
            bay: Some("A1".to_string()),
        },
        BusStatus {
            service: "Test Service 2".to_string(),
            bay: Some("B2".to_string()),
        },
    ];
    cache.set(test_data.clone()).await;

    let state = State((db, cache, None));
    let response = current_bus_information(state).await;
    let response = response.into_response();

    // Should return 200 OK
    assert_eq!(response.status(), 200);

    // Extract the body and parse as JSON
    let body = axum::body::to_bytes(response.into_body(), usize::MAX)
        .await
        .unwrap();
    let json: Value = serde_json::from_slice(&body).unwrap();

    // Should indicate cached data
    assert_eq!(json["cached"], true);

    // Should have our test data
    let buses = json["buses"].as_array().unwrap();
    assert_eq!(buses.len(), 2);

    // Verify the test data
    assert_eq!(buses[0]["service"], "Test Service 1");
    assert_eq!(buses[0]["bay"], "A1");
    assert_eq!(buses[1]["service"], "Test Service 2");
    assert_eq!(buses[1]["bay"], "B2");
}

#[tokio::test]
async fn test_current_bus_information_empty_cache() {
    let db = setup_test_database().await;
    let cache = setup_test_cache();

    // Set empty data in cache
    cache.set(vec![]).await;

    let state = State((db, cache, None));
    let response = current_bus_information(state).await;
    let response = response.into_response();

    // Should return 200 OK
    assert_eq!(response.status(), 200);

    // Extract the body and parse as JSON
    let body = axum::body::to_bytes(response.into_body(), usize::MAX)
        .await
        .unwrap();
    let json: Value = serde_json::from_slice(&body).unwrap();

    // Should indicate cached data
    assert_eq!(json["cached"], true);

    // Should have empty buses array
    let buses = json["buses"].as_array().unwrap();
    assert_eq!(buses.len(), 0);
}
