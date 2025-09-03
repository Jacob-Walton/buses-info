use anyhow::{Result, anyhow};
use base64::prelude::*;
use jsonwebtoken::{Algorithm, DecodingKey, Validation, decode};
use reqwest;
use rsa::{BigUint, RsaPublicKey, pkcs1::EncodeRsaPublicKey};
use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct GoogleTokenClaims {
    pub sub: String,
    pub email: String,
    pub name: String,
    pub given_name: String,
    pub family_name: String,
    pub picture: Option<String>,
    pub email_verified: bool,
    pub aud: String,
    pub iss: String,
    pub exp: usize,
    pub iat: usize,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct AppleTokenClaims {
    pub sub: String,
    pub email: String,
    pub email_verified: Option<bool>,
    pub aud: String,
    pub iss: String,
    pub exp: usize,
    pub iat: usize,
}

pub struct OAuthService {
    http_client: reqwest::Client,
}

impl Default for OAuthService {
    fn default() -> Self {
        Self::new()
    }
}

impl OAuthService {
    pub fn new() -> Self {
        Self {
            http_client: reqwest::Client::new(),
        }
    }

    pub async fn verify_google_token(&self, id_token: &str) -> Result<GoogleTokenClaims> {
        let jwks_url = "https://www.googleapis.com/oauth2/v3/certs";
        let response = self.http_client.get(jwks_url).send().await?;
        let jwks: serde_json::Value = response.json().await?;

        let header = jsonwebtoken::decode_header(id_token)?;
        let kid = header
            .kid
            .ok_or_else(|| anyhow!("No kid in token header"))?;

        let key = jwks["keys"]
            .as_array()
            .ok_or_else(|| anyhow!("Invalid JWKS format"))?
            .iter()
            .find(|k| k["kid"].as_str() == Some(&kid))
            .ok_or_else(|| anyhow!("Key not found in JWKS"))?;

        let n = key["n"]
            .as_str()
            .ok_or_else(|| anyhow!("Missing n parameter"))?;
        let e = key["e"]
            .as_str()
            .ok_or_else(|| anyhow!("Missing e parameter"))?;

        let n_bytes = BASE64_URL_SAFE_NO_PAD.decode(n)?;
        let e_bytes = BASE64_URL_SAFE_NO_PAD.decode(e)?;

        let public_key = RsaPublicKey::new(
            BigUint::from_bytes_be(&n_bytes),
            BigUint::from_bytes_be(&e_bytes),
        )?;

        let pem = public_key.to_pkcs1_pem(rsa::pkcs1::LineEnding::LF)?;
        let decoding_key = DecodingKey::from_rsa_pem(pem.as_bytes())?;

        let google_client_id = std::env::var("GOOGLE_CLIENT_ID")
            .map_err(|_| anyhow!("GOOGLE_CLIENT_ID environment variable not set"))?;

        let mut validation = Validation::new(Algorithm::RS256);
        validation.set_audience(&[&google_client_id]);
        validation.set_issuer(&["https://accounts.google.com"]);

        let token_data = decode::<GoogleTokenClaims>(id_token, &decoding_key, &validation)?;

        Ok(token_data.claims)
    }

    pub async fn verify_apple_token(&self, id_token: &str) -> Result<AppleTokenClaims> {
        let jwks_url = "https://appleid.apple.com/auth/keys";
        let response = self.http_client.get(jwks_url).send().await?;
        let jwks: serde_json::Value = response.json().await?;

        let header = jsonwebtoken::decode_header(id_token)?;
        let kid = header
            .kid
            .ok_or_else(|| anyhow!("No kid in token header"))?;

        let key = jwks["keys"]
            .as_array()
            .ok_or_else(|| anyhow!("Invalid JWKS format"))?
            .iter()
            .find(|k| k["kid"].as_str() == Some(&kid))
            .ok_or_else(|| anyhow!("Key not found in JWKS"))?;

        let n = key["n"]
            .as_str()
            .ok_or_else(|| anyhow!("Missing n parameter"))?;
        let e = key["e"]
            .as_str()
            .ok_or_else(|| anyhow!("Missing e parameter"))?;

        let n_bytes = BASE64_URL_SAFE_NO_PAD.decode(n)?;
        let e_bytes = BASE64_URL_SAFE_NO_PAD.decode(e)?;

        let public_key = RsaPublicKey::new(
            BigUint::from_bytes_be(&n_bytes),
            BigUint::from_bytes_be(&e_bytes),
        )?;

        let pem = public_key.to_pkcs1_pem(rsa::pkcs1::LineEnding::LF)?;
        let decoding_key = DecodingKey::from_rsa_pem(pem.as_bytes())?;

        let apple_client_id = std::env::var("APPLE_CLIENT_ID")
            .map_err(|_| anyhow!("APPLE_CLIENT_ID environment variable not set"))?;

        let mut validation = Validation::new(Algorithm::RS256);
        validation.validate_aud = false; // Apple tokens may have multiple audiences
        validation.set_issuer(&["https://appleid.apple.com"]);

        let token_data = decode::<AppleTokenClaims>(id_token, &decoding_key, &validation)?;

        if !token_data.claims.aud.contains(&apple_client_id)
            && token_data.claims.aud != apple_client_id
        {
            return Err(anyhow!(
                "Invalid audience in Apple token: got '{}', expected '{}'",
                token_data.claims.aud,
                apple_client_id
            ));
        }

        Ok(token_data.claims)
    }
}
