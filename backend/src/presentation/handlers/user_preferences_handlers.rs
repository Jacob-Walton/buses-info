use crate::domain::services::UserPreferencesService;
use axum::{
    Json,
    extract::{Path, Query},
    http::StatusCode,
    response::IntoResponse,
};
use serde::Deserialize;
use serde_json::json;
use std::sync::Arc;
use uuid::Uuid;

#[derive(Debug, Deserialize)]
pub struct AddFavoriteRequest {
    pub route_name: String,
}

#[derive(Debug, Deserialize)]
pub struct SetFavoritesRequest {
    pub routes: Vec<String>,
}

#[derive(Debug, Deserialize)]
pub struct SetPreferenceRequest {
    pub key: String,
    pub value: String,
}

#[derive(Debug, Deserialize)]
pub struct PreferenceQuery {
    pub key: String,
}

pub async fn get_favorite_routes(
    Path(user_id): Path<Uuid>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service.get_favorite_routes(user_id).await {
        Ok(response) => (StatusCode::OK, Json(json!(response))).into_response(),
        Err(e) => {
            tracing::error!("Failed to get favorite routes for user {}: {}", user_id, e);
            (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({
                    "error": "Failed to retrieve favorite routes"
                })),
            )
                .into_response()
        }
    }
}

pub async fn add_favorite_route(
    Path(user_id): Path<Uuid>,
    Json(request): Json<AddFavoriteRequest>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service
        .add_favorite_route(user_id, request.route_name)
        .await
    {
        Ok(_) => (
            StatusCode::CREATED,
            Json(json!({
                "message": "Route added to favorites"
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!("Failed to add favorite route for user {}: {}", user_id, e);
            (
                StatusCode::BAD_REQUEST,
                Json(json!({
                    "error": e.to_string()
                })),
            )
                .into_response()
        }
    }
}

pub async fn remove_favorite_route(
    Path((user_id, route_name)): Path<(Uuid, String)>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service
        .remove_favorite_route(user_id, route_name)
        .await
    {
        Ok(_) => (
            StatusCode::OK,
            Json(json!({
                "message": "Route removed from favorites"
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!(
                "Failed to remove favorite route for user {}: {}",
                user_id,
                e
            );
            (
                StatusCode::BAD_REQUEST,
                Json(json!({
                    "error": e.to_string()
                })),
            )
                .into_response()
        }
    }
}

pub async fn set_favorite_routes(
    Path(user_id): Path<Uuid>,
    Json(request): Json<SetFavoritesRequest>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service
        .set_favorite_routes(user_id, request.routes)
        .await
    {
        Ok(_) => (
            StatusCode::OK,
            Json(json!({
                "message": "Favorite routes updated"
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!("Failed to set favorite routes for user {}: {}", user_id, e);
            (
                StatusCode::BAD_REQUEST,
                Json(json!({
                    "error": e.to_string()
                })),
            )
                .into_response()
        }
    }
}

pub async fn toggle_favorite_route(
    Path((user_id, route_name)): Path<(Uuid, String)>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service
        .toggle_favorite_route(user_id, route_name)
        .await
    {
        Ok(added) => (StatusCode::OK, Json(json!({
            "added": added,
            "message": if added { "Route added to favorites" } else { "Route removed from favorites" }
        }))).into_response(),
        Err(e) => {
            tracing::error!("Failed to toggle favorite route for user {}: {}", user_id, e);
            (
                StatusCode::BAD_REQUEST,
                Json(json!({
                    "error": e.to_string()
                })),
            )
                .into_response()
        }
    }
}

pub async fn set_preference(
    Path(user_id): Path<Uuid>,
    Json(request): Json<SetPreferenceRequest>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service
        .set_preference(user_id, request.key, request.value)
        .await
    {
        Ok(_) => (
            StatusCode::OK,
            Json(json!({
                "message": "Preference updated"
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!("Failed to set preference for user {}: {}", user_id, e);
            (
                StatusCode::BAD_REQUEST,
                Json(json!({
                    "error": e.to_string()
                })),
            )
                .into_response()
        }
    }
}

pub async fn get_preference(
    Path(user_id): Path<Uuid>,
    Query(query): Query<PreferenceQuery>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service.get_preference(user_id, query.key).await {
        Ok(value) => (
            StatusCode::OK,
            Json(json!({
                "value": value
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!("Failed to get preference for user {}: {}", user_id, e);
            (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({
                    "error": "Failed to retrieve preference"
                })),
            )
                .into_response()
        }
    }
}

pub async fn get_all_preferences(
    Path(user_id): Path<Uuid>,
    preferences_service: Arc<UserPreferencesService>,
) -> impl IntoResponse {
    match preferences_service.get_all_preferences(user_id).await {
        Ok(preferences) => (
            StatusCode::OK,
            Json(json!({
                "preferences": preferences
            })),
        )
            .into_response(),
        Err(e) => {
            tracing::error!("Failed to get all preferences for user {}: {}", user_id, e);
            (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(json!({
                    "error": "Failed to retrieve preferences"
                })),
            )
                .into_response()
        }
    }
}
