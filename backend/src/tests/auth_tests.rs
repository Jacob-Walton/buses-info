use crate::auth::{
    Claims, User, authenticate_user, create_user, generate_token, get_user_by_email,
    get_user_by_id, hash_password, verify_password, verify_token,
};
use crate::tests::{init_test_env, setup_test_database};
use chrono::Utc;

#[tokio::test]
async fn test_password_hashing() {
    let password = "test_password_123";
    let hash = hash_password(password).expect("Failed to hash password");

    assert!(verify_password(password, &hash).expect("Failed to verify password"));
    assert!(!verify_password("wrong_password", &hash).expect("Failed to verify password"));
}

#[tokio::test]
async fn test_jwt_token_generation_and_verification() {
    init_test_env();

    let user = User {
        id: "test-user-id".to_string(),
        email: "test@example.com".to_string(),
        first_name: "Test".to_string(),
        last_name: "User".to_string(),
        password_hash: "hash".to_string(),
        role: "user".to_string(),
        created_at: Utc::now(),
    };

    let token = generate_token(&user).expect("Failed to generate token");
    let claims = verify_token(&token).expect("Failed to verify token");

    assert_eq!(claims.sub, user.id);
    assert_eq!(claims.email, user.email);
    assert_eq!(claims.role, user.role);
}

#[tokio::test]
async fn test_invalid_jwt_token() {
    init_test_env();

    let invalid_token = "invalid.jwt.token";
    assert!(verify_token(invalid_token).is_err());
}

#[tokio::test]
async fn test_create_and_get_user() {
    let db = setup_test_database().await;

    let email = "newuser@example.com";
    let first_name = "New";
    let last_name = "User";
    let password_hash = hash_password("password123").unwrap();

    let created_user = create_user(&db, email, first_name, last_name, &password_hash)
        .await
        .expect("Failed to create user");

    assert_eq!(created_user.email, email);
    assert_eq!(created_user.first_name, first_name);
    assert_eq!(created_user.last_name, last_name);
    assert_eq!(created_user.role, "user");

    // Test get by email
    let found_user = get_user_by_email(&db, email)
        .await
        .expect("Failed to get user by email")
        .expect("User not found");

    assert_eq!(found_user.id, created_user.id);
    assert_eq!(found_user.email, created_user.email);

    // Test get by ID
    let found_user_by_id = get_user_by_id(&db, &created_user.id)
        .await
        .expect("Failed to get user by ID")
        .expect("User not found");

    assert_eq!(found_user_by_id.id, created_user.id);
}

#[tokio::test]
async fn test_get_nonexistent_user() {
    let db = setup_test_database().await;

    let result = get_user_by_email(&db, "nonexistent@example.com").await;
    assert!(result.is_ok());
    assert!(result.unwrap().is_none());

    let result = get_user_by_id(&db, "00000000-0000-0000-0000-000000000000").await;
    assert!(result.is_ok());
    assert!(result.unwrap().is_none());
}

#[tokio::test]
async fn test_authenticate_user() {
    init_test_env();
    let db = setup_test_database().await;

    // Create a user
    let email = "auth_test@example.com";
    let password_hash = hash_password("password123").unwrap();
    let user = create_user(&db, email, "Auth", "Test", &password_hash)
        .await
        .expect("Failed to create user");

    // Generate token
    let token = generate_token(&user).expect("Failed to generate token");

    // Authenticate with token
    let authenticated_user = authenticate_user(&db, &token)
        .await
        .expect("Failed to authenticate user");

    assert_eq!(authenticated_user.id, user.id);
    assert_eq!(authenticated_user.email, user.email);
}

#[tokio::test]
async fn test_authenticate_user_invalid_token() {
    init_test_env();
    let db = setup_test_database().await;

    let result = authenticate_user(&db, "invalid.token").await;
    assert!(result.is_err());
}

#[tokio::test]
async fn test_claims_creation() {
    let user = User {
        id: "test-user-id".to_string(),
        email: "test@example.com".to_string(),
        first_name: "Test".to_string(),
        last_name: "User".to_string(),
        password_hash: "hash".to_string(),
        role: "admin".to_string(),
        created_at: Utc::now(),
    };

    let claims = Claims::new(&user);

    assert_eq!(claims.sub, user.id);
    assert_eq!(claims.email, user.email);
    assert_eq!(claims.role, user.role);
    assert!(claims.exp > 0);
}

#[tokio::test]
async fn test_user_to_response() {
    let user = User {
        id: "test-user-id".to_string(),
        email: "test@example.com".to_string(),
        first_name: "Test".to_string(),
        last_name: "User".to_string(),
        password_hash: "hash".to_string(),
        role: "user".to_string(),
        created_at: Utc::now(),
    };

    let response = user.to_response();

    assert_eq!(response.id, user.id);
    assert_eq!(response.email, user.email);
    assert_eq!(response.name, "Test User");
    assert_eq!(response.role, user.role);
}
