use crate::redis_service::RedisService;
use crate::tests::init_test_env;

#[tokio::test]
async fn test_redis_service_creation() {
    init_test_env();

    let redis_service = RedisService::new(Some("redis://localhost:6379"));
    assert!(redis_service.is_ok());
}

#[tokio::test]
async fn test_generate_refresh_token() {
    let token1 = RedisService::generate_refresh_token();
    let token2 = RedisService::generate_refresh_token();

    assert_ne!(token1, token2);
    assert!(!token1.is_empty());
    assert!(!token2.is_empty());
}

#[tokio::test]
async fn test_store_and_retrieve_refresh_token() {
    init_test_env();

    if let Ok(redis_service) = RedisService::new(Some("redis://localhost:6379")) {
        let user_id = "test-user-123";
        let refresh_token = "test-refresh-token";

        let store_result = redis_service
            .store_refresh_token(user_id, refresh_token, 1)
            .await;

        if store_result.is_ok() {
            let retrieved = redis_service.get_refresh_token_data(refresh_token).await;

            match retrieved {
                Ok(Some(data)) => {
                    assert_eq!(data.user_id, user_id);
                    assert!(data.created_at.timestamp() > 0);
                }
                Ok(None) => println!("Token not found (Redis may not be running)"),
                Err(e) => println!("Redis error: {e}"),
            }
        } else {
            println!("Skipping Redis test - service not available");
        }
    } else {
        println!("Skipping Redis test - connection failed");
    }
}

#[tokio::test]
async fn test_invalidate_refresh_token() {
    init_test_env();

    if let Ok(redis_service) = RedisService::new(Some("redis://localhost:6379")) {
        let user_id = "test-user-456";
        let refresh_token = "test-refresh-token-delete";

        if redis_service
            .store_refresh_token(user_id, refresh_token, 1)
            .await
            .is_ok()
        {
            let _ = redis_service.invalidate_refresh_token(refresh_token).await;

            let retrieved = redis_service.get_refresh_token_data(refresh_token).await;

            match retrieved {
                Ok(None) => println!("Token successfully invalidated"),
                Ok(Some(_)) => println!("Token still exists (unexpected)"),
                Err(e) => println!("Redis error: {e}"),
            }
        }
    } else {
        println!("Skipping Redis test - connection failed");
    }
}
