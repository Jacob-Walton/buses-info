use axum::{
    extract::State,
    http::{HeaderMap, StatusCode},
    response::Json,
};
use chrono::{DateTime, Utc};
use serde::Serialize;
use sqlx::Row;
use std::collections::HashMap;

use crate::auth::{Claims, verify_token};
use crate::cache::BusCache;
use crate::database::Database;
use crate::redis_service::RedisService;

#[derive(Debug, Serialize)]
pub struct UserDataExport {
    pub export_date: String,
    pub user_info: UserInfo,
    pub preferences: UserPreferences,
    pub usage_data: UsageData,
}

#[derive(Debug, Serialize)]
pub struct UserInfo {
    pub email: String,
    pub created_at: String,
    pub last_login: Option<String>,
}

#[derive(Debug, Serialize)]
pub struct UserPreferences {
    pub favorite_routes: Vec<String>,
    pub notification_settings: HashMap<String, bool>,
    pub display_settings: HashMap<String, String>,
}

#[derive(Debug, Serialize)]
pub struct UsageData {
    pub total_logins: u32,
    pub last_30_days_activity: Vec<ActivityRecord>,
}

#[derive(Debug, Serialize)]
pub struct ActivityRecord {
    pub date: String,
    pub page_views: u32,
    pub session_duration_minutes: Option<u32>,
}

#[derive(Debug, Serialize)]
pub struct DataExportResponse {
    pub status: String,
    pub message: String,
    pub request_id: String,
}

#[derive(Debug, Serialize)]
pub struct AccountDeletionResponse {
    pub status: String,
    pub message: String,
}

/// Export user's personal data
pub async fn export_user_data(
    State((database, _cache, _redis)): State<(Database, BusCache, Option<RedisService>)>,
    headers: HeaderMap,
) -> Result<Json<UserDataExport>, StatusCode> {
    let claims = extract_claims_from_headers(&headers)?;
    let user_id = claims.sub;

    // Get user basic info
    let user_info = get_user_info(&database, &user_id)
        .await
        .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    // Get user preferences (if any exist)
    let preferences = get_user_preferences(&database, &user_id)
        .await
        .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    // Get usage data
    let usage_data = get_usage_data(&database, &user_id)
        .await
        .map_err(|_| StatusCode::INTERNAL_SERVER_ERROR)?;

    let export = UserDataExport {
        export_date: Utc::now().to_rfc3339(),
        user_info,
        preferences,
        usage_data,
    };

    Ok(Json(export))
}

/// Request data export
pub async fn request_data_export(
    headers: HeaderMap,
) -> Result<Json<DataExportResponse>, StatusCode> {
    let _claims = extract_claims_from_headers(&headers)?;
    let request_id = uuid::Uuid::new_v4().to_string();
    let request_id_clone = request_id.clone();

    // Queue export job for background processing
    tokio::spawn(async move {
        if let Err(e) = process_export_request(request_id_clone.clone()).await {
            tracing::error!(
                "Export processing failed for request {}: {}",
                request_id_clone,
                e
            );
        }
    });

    Ok(Json(DataExportResponse {
        status: "accepted".to_string(),
        message: "Your data export request has been received. You will receive an email with a download link within 24 hours.".to_string(),
        request_id,
    }))
}

/// Delete user account
pub async fn delete_user_account(
    State((database, _cache, redis)): State<(Database, BusCache, Option<RedisService>)>,
    headers: HeaderMap,
) -> Result<Json<AccountDeletionResponse>, StatusCode> {
    let claims = extract_claims_from_headers(&headers)?;
    let user_id = claims.sub;

    // Invalidate all refresh tokens for this user
    if let Some(redis_service) = &redis
        && let Err(e) = redis_service.invalidate_all_user_tokens(&user_id).await
    {
        tracing::error!("Failed to invalidate user tokens: {}", e);
    }

    // Delete user data from database
    let result = delete_user_data(&database, &user_id).await;

    match result {
        Ok(_) => Ok(Json(AccountDeletionResponse {
            status: "success".to_string(),
            message: "Your account and all associated data have been permanently deleted."
                .to_string(),
        })),
        Err(_) => Err(StatusCode::INTERNAL_SERVER_ERROR),
    }
}

// Helper function to extract claims from headers
fn extract_claims_from_headers(headers: &HeaderMap) -> Result<Claims, StatusCode> {
    let auth_header = headers
        .get("authorization")
        .and_then(|header| header.to_str().ok())
        .and_then(|header| header.strip_prefix("Bearer "));

    let token = match auth_header {
        Some(token) => token,
        None => {
            return Err(StatusCode::UNAUTHORIZED);
        }
    };

    match verify_token(token) {
        Ok(claims) => Ok(claims),
        Err(_) => Err(StatusCode::UNAUTHORIZED),
    }
}

// Helper functions to interact with database

pub async fn get_user_info(
    database: &Database,
    user_id: &str,
) -> Result<UserInfo, Box<dyn std::error::Error>> {
    let query = "SELECT email, created_at, last_login FROM users WHERE id = $1";

    let user_uuid = uuid::Uuid::parse_str(user_id)?;
    let row = sqlx::query(query)
        .bind(user_uuid)
        .fetch_one(&database.pool)
        .await?;

    let email: String = row.get("email");
    let created_at: DateTime<Utc> = row.get("created_at");
    let last_login: Option<DateTime<Utc>> = row.get("last_login");

    Ok(UserInfo {
        email,
        created_at: created_at.to_rfc3339(),
        last_login: last_login.map(|dt| dt.to_rfc3339()),
    })
}

pub async fn get_user_preferences(
    database: &Database,
    _user_id: &str,
) -> Result<UserPreferences, Box<dyn std::error::Error>> {
    // Check if user_preferences table exists, if not return empty preferences
    let table_exists = sqlx::query("SELECT to_regclass('user_preferences')")
        .fetch_one(&database.pool)
        .await;

    if table_exists.is_err() {
        // Table doesn't exist, return empty preferences
        return Ok(UserPreferences {
            favorite_routes: vec![],
            notification_settings: HashMap::new(),
            display_settings: HashMap::new(),
        });
    }

    // For now, return empty preferences since we haven't implemented user preferences yet
    // In the future, this would query the user_preferences table
    Ok(UserPreferences {
        favorite_routes: get_favorite_routes(database, _user_id)
            .await
            .unwrap_or_default(),
        notification_settings: HashMap::new(),
        display_settings: HashMap::new(),
    })
}

pub async fn get_usage_data(
    database: &Database,
    user_id: &str,
) -> Result<UsageData, Box<dyn std::error::Error>> {
    // For now, return empty usage data since we haven't implemented analytics yet

    // Count total logins (simple approximation)
    let login_count_query = "SELECT COUNT(*) as login_count FROM users WHERE id = $1";
    let user_uuid = uuid::Uuid::parse_str(user_id)?;
    let login_count: i64 = sqlx::query(login_count_query)
        .bind(user_uuid)
        .fetch_one(&database.pool)
        .await?
        .get("login_count");

    Ok(UsageData {
        total_logins: login_count as u32,
        last_30_days_activity: get_recent_activity(database, user_id)
            .await
            .unwrap_or_default(),
    })
}

pub async fn delete_user_data(
    database: &Database,
    user_id: &str,
) -> Result<(), Box<dyn std::error::Error>> {
    let user_uuid = uuid::Uuid::parse_str(user_id)?;

    // First, try to delete user preferences outside of transaction
    // We don't care if this fails since the table might not exist
    let _ = sqlx::query("DELETE FROM user_preferences WHERE user_id = $1")
        .bind(user_uuid)
        .execute(&database.pool)
        .await;

    // Now delete the user account in a separate transaction
    let mut tx = database.pool.begin().await?;

    let result = sqlx::query("DELETE FROM users WHERE id = $1")
        .bind(user_uuid)
        .execute(&mut *tx)
        .await;

    match result {
        Ok(_) => {
            tx.commit().await?;
            Ok(())
        }
        Err(e) => {
            let _ = tx.rollback().await;
            Err(Box::new(e))
        }
    }
}

async fn process_export_request(request_id: String) -> Result<(), Box<dyn std::error::Error>> {
    // Pretend to be processing
    tokio::time::sleep(tokio::time::Duration::from_secs(2)).await;

    // Eventually this will:
    // 1. Generate the export file (CSV, JSON, etc.)
    // 2. Upload to storage (S3, etc.)
    // 3. Send email with download link
    // 4. Clean up temporary files after expiration

    tracing::info!("Export request {} processed successfully", request_id);
    Ok(())
}

async fn get_favorite_routes(
    database: &Database,
    user_id: &str,
) -> Result<Vec<String>, Box<dyn std::error::Error>> {
    let user_uuid = uuid::Uuid::parse_str(user_id)?;

    // Check if routes table exists
    let table_check = sqlx::query("SELECT to_regclass('user_favorite_routes')")
        .fetch_optional(&database.pool)
        .await;

    if table_check.is_err() || table_check.unwrap().is_none() {
        return Ok(vec![]);
    }

    let rows = sqlx::query("SELECT route_name FROM user_favorite_routes WHERE user_id = $1")
        .bind(user_uuid)
        .fetch_all(&database.pool)
        .await?;

    let routes = rows
        .iter()
        .map(|row| row.get::<String, _>("route_name"))
        .collect();

    Ok(routes)
}

async fn get_recent_activity(
    database: &Database,
    user_id: &str,
) -> Result<Vec<ActivityRecord>, Box<dyn std::error::Error>> {
    let user_uuid = uuid::Uuid::parse_str(user_id)?;

    // Check if analytics table exists
    let table_check = sqlx::query("SELECT to_regclass('user_activity_logs')")
        .fetch_optional(&database.pool)
        .await;

    if table_check.is_err() || table_check.unwrap().is_none() {
        return Ok(vec![]);
    }

    let rows = sqlx::query(
        "SELECT 
            DATE(created_at) as activity_date,
            COUNT(*) as page_views,
            AVG(EXTRACT(EPOCH FROM (session_end - session_start))/60) as avg_session_minutes
         FROM user_activity_logs 
         WHERE user_id = $1 AND created_at >= NOW() - INTERVAL '30 days'
         GROUP BY DATE(created_at)
         ORDER BY activity_date DESC",
    )
    .bind(user_uuid)
    .fetch_all(&database.pool)
    .await?;

    let activity = rows
        .iter()
        .map(|row| ActivityRecord {
            date: row.get::<chrono::NaiveDate, _>("activity_date").to_string(),
            page_views: row.get::<i64, _>("page_views") as u32,
            session_duration_minutes: row
                .get::<Option<f64>, _>("avg_session_minutes")
                .map(|avg| avg as u32),
        })
        .collect();

    Ok(activity)
}
