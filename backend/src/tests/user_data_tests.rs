use crate::auth::{create_user, hash_password};
use crate::tests::setup_test_database;
use crate::user_data::{delete_user_data, get_usage_data, get_user_info, get_user_preferences};

#[tokio::test]
async fn test_get_user_info() {
    let db = setup_test_database().await;

    // Create a test user
    let password_hash = hash_password("password123").unwrap();
    let user = create_user(&db, "userdata@example.com", "User", "Data", &password_hash)
        .await
        .expect("Failed to create user");

    // Get user info
    let user_info = get_user_info(&db, &user.id)
        .await
        .expect("Failed to get user info");

    assert_eq!(user_info.email, user.email);
    assert!(!user_info.created_at.is_empty());
    assert!(user_info.last_login.is_none()); // New user, no login yet
}

#[tokio::test]
async fn test_get_user_preferences() {
    let db = setup_test_database().await;

    // Since preferences table doesn't exist yet, should return empty preferences
    let preferences = get_user_preferences(&db, "test-user-id")
        .await
        .expect("Failed to get user preferences");

    assert!(preferences.favorite_routes.is_empty());
    assert!(preferences.notification_settings.is_empty());
    assert!(preferences.display_settings.is_empty());
}

#[tokio::test]
async fn test_get_usage_data() {
    let db = setup_test_database().await;

    // Create a test user
    let password_hash = hash_password("password123").unwrap();
    let user = create_user(&db, "usage@example.com", "Usage", "Test", &password_hash)
        .await
        .expect("Failed to create user");

    let usage_data = get_usage_data(&db, &user.id)
        .await
        .expect("Failed to get usage data");

    assert_eq!(usage_data.total_logins, 1); // User was just created
    assert!(usage_data.last_30_days_activity.is_empty());
}

#[tokio::test]
async fn test_delete_user_data() {
    let db = setup_test_database().await;

    // Create a test user
    let password_hash = hash_password("password123").unwrap();
    let user = create_user(&db, "delete@example.com", "Delete", "Test", &password_hash)
        .await
        .expect("Failed to create user");

    // Verify user exists
    let user_exists_before = get_user_info(&db, &user.id).await;
    assert!(
        user_exists_before.is_ok(),
        "User should exist before deletion"
    );

    // Delete user data
    let delete_result = delete_user_data(&db, &user.id).await;
    if let Err(e) = &delete_result {
        println!("Delete error: {e}");
    }
    assert!(delete_result.is_ok(), "User deletion should succeed");

    // Verify user is deleted
    let user_exists_after = get_user_info(&db, &user.id).await;
    assert!(
        user_exists_after.is_err(),
        "User should not exist after deletion"
    );
}

#[tokio::test]
async fn test_delete_nonexistent_user() {
    let db = setup_test_database().await;

    // Try to delete a user that doesn't exist
    let result = delete_user_data(&db, "00000000-0000-0000-0000-000000000000").await;

    // Should succeed (no error) even if user doesn't exist
    assert!(
        result.is_ok(),
        "Deleting non-existent user should succeed (no-op)"
    );
}
