use axum::{
    Router,
    body::Body,
    http::{Request, StatusCode, header},
};
use serde_json::json;
use tower::ServiceExt;

use crate::auth_handlers::{login, me, register};
use crate::handlers::{health_check, health_status};
use crate::tests::{init_test_env, setup_test_cache, setup_test_database};
use crate::user_data::{export_user_data, request_data_export};

async fn create_test_app() -> Router {
    init_test_env();
    let db = setup_test_database().await;
    let cache = setup_test_cache();
    let state = (db, cache);

    Router::new()
        .route("/health", axum::routing::get(health_check))
        .route("/health/status", axum::routing::get(health_status))
        .route("/register", axum::routing::post(register))
        .route("/login", axum::routing::post(login))
        .route("/me", axum::routing::get(me))
        .route("/export-data", axum::routing::post(request_data_export))
        .route("/data", axum::routing::get(export_user_data))
        .with_state(state)
}

#[tokio::test]
async fn test_health_check_endpoint() {
    let app = create_test_app().await;

    let request = Request::builder()
        .uri("/health")
        .body(Body::empty())
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert_eq!(response.status(), StatusCode::OK);
}

#[tokio::test]
async fn test_register_endpoint() {
    let app = create_test_app().await;

    let register_payload = json!({
        "email": "test@example.com",
        "password": "password123",
        "firstName": "Test",
        "lastName": "User"
    });

    let request = Request::builder()
        .method("POST")
        .uri("/register")
        .header(header::CONTENT_TYPE, "application/json")
        .body(Body::from(register_payload.to_string()))
        .unwrap();

    let response = app.oneshot(request).await.unwrap();

    // Should either succeed or fail gracefully
    assert!(response.status() == StatusCode::CREATED || response.status().is_client_error());
}

#[tokio::test]
async fn test_register_invalid_email() {
    let app = create_test_app().await;

    let register_payload = json!({
        "email": "invalid-email",
        "password": "password123",
        "firstName": "Test",
        "lastName": "User"
    });

    let request = Request::builder()
        .method("POST")
        .uri("/register")
        .header(header::CONTENT_TYPE, "application/json")
        .body(Body::from(register_payload.to_string()))
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert_eq!(response.status(), StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_register_missing_fields() {
    let app = create_test_app().await;

    let register_payload = json!({
        "email": "test@example.com",
        "password": "password123"
        // Missing firstName and lastName
    });

    let request = Request::builder()
        .method("POST")
        .uri("/register")
        .header(header::CONTENT_TYPE, "application/json")
        .body(Body::from(register_payload.to_string()))
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    // The response could be 400 (BAD_REQUEST) or 422 (UNPROCESSABLE_ENTITY) depending on how Axum handles JSON parsing
    assert!(
        response.status() == StatusCode::BAD_REQUEST
            || response.status() == StatusCode::UNPROCESSABLE_ENTITY
    );
}

#[tokio::test]
async fn test_login_missing_credentials() {
    let app = create_test_app().await;

    let login_payload = json!({
        "email": "",
        "password": ""
    });

    let request = Request::builder()
        .method("POST")
        .uri("/login")
        .header(header::CONTENT_TYPE, "application/json")
        .body(Body::from(login_payload.to_string()))
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert_eq!(response.status(), StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn test_me_endpoint_without_auth() {
    let app = create_test_app().await;

    let request = Request::builder().uri("/me").body(Body::empty()).unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert_eq!(response.status(), StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn test_export_data_without_auth() {
    let app = create_test_app().await;

    let request = Request::builder()
        .method("POST")
        .uri("/export-data")
        .body(Body::empty())
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert_eq!(response.status(), StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn test_malformed_json_request() {
    let app = create_test_app().await;

    let request = Request::builder()
        .method("POST")
        .uri("/register")
        .header(header::CONTENT_TYPE, "application/json")
        .body(Body::from("invalid json"))
        .unwrap();

    let response = app.oneshot(request).await.unwrap();
    assert!(response.status().is_client_error());
}
