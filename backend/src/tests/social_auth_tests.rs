use crate::auth::{AppleTokenClaims, GoogleTokenClaims, verify_apple_token, verify_google_token};
use crate::tests::init_test_env;

#[tokio::test]
async fn test_google_token_verification_invalid_token() {
    init_test_env();

    let invalid_token = "invalid.jwt.token";
    let result = verify_google_token(invalid_token).await;

    assert!(result.is_err());
}

#[tokio::test]
async fn test_apple_token_verification_invalid_token() {
    init_test_env();

    let invalid_token = "invalid.jwt.token";
    let result = verify_apple_token(invalid_token).await;

    assert!(result.is_err());
}

#[tokio::test]
async fn test_google_token_verification_malformed() {
    init_test_env();

    let malformed_token = "not-a-jwt-at-all";
    let result = verify_google_token(malformed_token).await;

    assert!(result.is_err());
}

#[tokio::test]
async fn test_apple_token_verification_malformed() {
    init_test_env();

    let malformed_token = "not-a-jwt-at-all";
    let result = verify_apple_token(malformed_token).await;

    assert!(result.is_err());
}
