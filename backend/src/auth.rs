use anyhow::{Result, anyhow};
use bcrypt::{DEFAULT_COST, hash, verify};
use chrono::{Duration, Utc};
use jsonwebtoken::{DecodingKey, EncodingKey, Header, Validation, decode, encode};
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use sqlx::Row;
use tracing::{debug, error, warn};

use crate::database::Database;

static JWT_SECRET: Lazy<Option<Vec<u8>>> = Lazy::new(|| {
    std::env::var("JWT_SECRET_KEY")
        .ok()
        .map(|v| v.as_bytes().to_vec())
});

#[derive(Debug, Serialize, Deserialize)]
pub struct Claims {
    pub sub: String, // user id
    pub email: String,
    pub role: String,
    pub exp: usize,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct User {
    pub id: String,
    pub email: String,
    pub first_name: String,
    pub last_name: String,
    pub password_hash: String,
    pub role: String,
    pub created_at: chrono::DateTime<chrono::Utc>,
}

#[derive(Debug, Serialize)]
pub struct UserResponse {
    pub id: String,
    pub email: String,
    pub name: String,
    pub role: String,
}

#[derive(Debug, Deserialize)]
pub struct LoginRequest {
    pub email: String,
    pub password: String,
}

#[derive(Debug, Deserialize)]
pub struct RegisterRequest {
    pub email: String,
    pub password: String,
    #[serde(rename = "firstName")]
    pub first_name: String,
    #[serde(rename = "lastName")]
    pub last_name: String,
}

#[derive(Debug, Serialize)]
pub struct AuthResponse {
    pub user: UserResponse,
    pub token: String,
}

impl User {
    pub fn to_response(&self) -> UserResponse {
        UserResponse {
            id: self.id.clone(),
            email: self.email.clone(),
            name: format!("{} {}", self.first_name, self.last_name),
            role: self.role.clone(),
        }
    }
}

impl Claims {
    pub fn new(user: &User) -> Self {
        let exp = (Utc::now() + Duration::days(7)).timestamp() as usize;
        Self {
            sub: user.id.clone(),
            email: user.email.clone(),
            role: user.role.clone(),
            exp,
        }
    }
}

pub fn generate_token(user: &User) -> Result<String> {
    let claims = Claims::new(user);
    let secret = JWT_SECRET
        .as_ref()
        .ok_or_else(|| anyhow!("JWT_SECRET_KEY environment variable is not set"))?;
    let token = encode(
        &Header::default(),
        &claims,
        &EncodingKey::from_secret(secret),
    )
    .map_err(|e| anyhow!("Failed to generate token: {}", e))?;
    Ok(token)
}

pub fn verify_token(token: &str) -> Result<Claims> {
    let secret = JWT_SECRET
        .as_ref()
        .ok_or_else(|| anyhow!("JWT_SECRET_KEY environment variable is not set"))?;
    let token_data = decode::<Claims>(
        token,
        &DecodingKey::from_secret(secret),
        &Validation::default(),
    )
    .map_err(|e| anyhow!("Invalid token: {}", e))?;

    Ok(token_data.claims)
}

pub fn hash_password(password: &str) -> Result<String> {
    hash(password, DEFAULT_COST).map_err(|e| anyhow!("Failed to hash password: {}", e))
}

pub fn verify_password(password: &str, hash: &str) -> Result<bool> {
    verify(password, hash).map_err(|e| anyhow!("Failed to verify password: {}", e))
}

pub async fn authenticate_user(database: &Database, token: &str) -> Result<User, anyhow::Error> {
    let claims = verify_token(token)?;

    // Fetch user from database
    match get_user_by_id(database, &claims.sub).await? {
        Some(user) => Ok(user),
        None => Err(anyhow!("User not found")),
    }
}

pub async fn get_user_by_id(db: &Database, user_id: &str) -> Result<Option<User>> {
    let user_uuid = uuid::Uuid::parse_str(user_id)?;

    let row = sqlx::query(
        "SELECT id, email, first_name, last_name, password_hash, role, created_at FROM users WHERE id = $1"
    )
    .bind(user_uuid)
    .fetch_optional(&db.pool)
    .await?;

    match row {
        Some(row) => {
            let id: uuid::Uuid = row.get("id");
            let email: String = row.get("email");
            let first_name: String = row.get("first_name");
            let last_name: String = row.get("last_name");
            let password_hash: String = row.get("password_hash");
            let role: String = row.get("role");
            let created_at: chrono::DateTime<chrono::Utc> = row.get("created_at");

            Ok(Some(User {
                id: id.to_string(),
                email,
                first_name,
                last_name,
                password_hash,
                role,
                created_at,
            }))
        }
        None => Ok(None),
    }
}

pub async fn get_user_by_email(db: &Database, email: &str) -> Result<Option<User>> {
    let row = sqlx::query(
        "SELECT id, email, first_name, last_name, password_hash, role, created_at FROM users WHERE email = $1"
    )
    .bind(email)
    .fetch_optional(&db.pool)
    .await?;

    match row {
        Some(row) => {
            let id: uuid::Uuid = row.get("id");
            let email: String = row.get("email");
            let first_name: String = row.get("first_name");
            let last_name: String = row.get("last_name");
            let password_hash: String = row.get("password_hash");
            let role: String = row.get("role");
            let created_at: chrono::DateTime<chrono::Utc> = row.get("created_at");

            Ok(Some(User {
                id: id.to_string(),
                email,
                first_name,
                last_name,
                password_hash,
                role,
                created_at,
            }))
        }
        None => Ok(None),
    }
}

pub async fn create_user(
    db: &Database,
    email: &str,
    first_name: &str,
    last_name: &str,
    password_hash: &str,
) -> Result<User> {
    let user_id = uuid::Uuid::new_v4();
    let now = chrono::Utc::now();

    let row = sqlx::query(
        "INSERT INTO users (id, email, first_name, last_name, password_hash, role, created_at) 
         VALUES ($1, $2, $3, $4, $5, $6, $7) 
         RETURNING id, email, first_name, last_name, password_hash, role, created_at",
    )
    .bind(user_id)
    .bind(email)
    .bind(first_name)
    .bind(last_name)
    .bind(password_hash)
    .bind("user")
    .bind(now)
    .fetch_one(&db.pool)
    .await?;

    let id: uuid::Uuid = row.get("id");
    let email: String = row.get("email");
    let first_name: String = row.get("first_name");
    let last_name: String = row.get("last_name");
    let password_hash: String = row.get("password_hash");
    let role: String = row.get("role");
    let created_at: chrono::DateTime<chrono::Utc> = row.get("created_at");

    Ok(User {
        id: id.to_string(),
        email,
        first_name,
        last_name,
        password_hash,
        role,
        created_at,
    })
}

pub async fn create_test_users(db: &Database) {
    let users = vec![
        RegisterRequest {
            email: "test@example.com".to_string(),
            password: "password".to_string(),
            first_name: "Test".to_string(),
            last_name: "User".to_string(),
        },
        RegisterRequest {
            email: "admin@example.com".to_string(),
            password: "admin".to_string(),
            first_name: "Admin".to_string(),
            last_name: "User".to_string(),
        },
    ];

    for user in users {
        if let Ok(existing_user) = get_user_by_email(db, &user.email).await {
            if existing_user.is_none() {
                let password_hash = hash_password(&user.password).unwrap();
                let new_user = create_user(
                    db,
                    &user.email,
                    &user.first_name,
                    &user.last_name,
                    &password_hash,
                )
                .await
                .unwrap();
                debug!("Created user: {:?}", new_user);
            } else {
                warn!("User with email {} already exists", user.email);
            }
        } else {
            error!("Failed to check for existing user with email {}", user.email);
        }
    }
}