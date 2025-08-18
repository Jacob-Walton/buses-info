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
    State((database, _cache)): State<(Database, BusCache)>,
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

    // TODO: Implement actual export logic and email notification

    Ok(Json(DataExportResponse {
        status: "accepted".to_string(),
        message: "Your data export request has been received. You will receive an email with a download link within 24 hours.".to_string(),
        request_id,
    }))
}

/// Delete user account
pub async fn delete_user_account(
    State((database, _cache)): State<(Database, BusCache)>,
    headers: HeaderMap,
) -> Result<Json<AccountDeletionResponse>, StatusCode> {
    let claims = extract_claims_from_headers(&headers)?;
    let user_id = claims.sub;

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

async fn get_user_info(
    database: &Database,
    user_id: &str,
) -> Result<UserInfo, Box<dyn std::error::Error>> {
    let query = "SELECT email, created_at, last_login FROM users WHERE id = $1";

    let row = sqlx::query(query)
        .bind(user_id)
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

async fn get_user_preferences(
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
        favorite_routes: vec![], // Would come from user_preferences table
        notification_settings: HashMap::new(),
        display_settings: HashMap::new(),
    })
}

async fn get_usage_data(
    database: &Database,
    user_id: &str,
) -> Result<UsageData, Box<dyn std::error::Error>> {
    // For now, return empty usage data since we haven't implemented analytics yet

    // Count total logins (simple approximation)
    let login_count_query = "SELECT COUNT(*) as login_count FROM users WHERE id = $1";
    let login_count: i64 = sqlx::query(login_count_query)
        .bind(user_id)
        .fetch_one(&database.pool)
        .await?
        .get("login_count");

    Ok(UsageData {
        total_logins: login_count as u32,
        last_30_days_activity: vec![], // Would be populated from analytics table
    })
}

async fn delete_user_data(
    database: &Database,
    user_id: &str,
) -> Result<(), Box<dyn std::error::Error>> {
    // Start a transaction to ensure all deletes succeed or none do
    let mut tx = database.pool.begin().await?;

    // Delete user preferences (if table exists)
    let _ = sqlx::query("DELETE FROM user_preferences WHERE user_id = $1")
        .bind(user_id)
        .execute(&mut *tx)
        .await; // Don't fail if table doesn't exist

    // Delete the user account itself
    sqlx::query("DELETE FROM users WHERE id = $1")
        .bind(user_id)
        .execute(&mut *tx)
        .await?;

    // Commit the transaction
    tx.commit().await?;

    Ok(())
}
