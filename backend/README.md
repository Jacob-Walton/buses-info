# Backend (Rust API)

Rust backend providing REST API for bus arrival data and authentication.

## Quick Start

See the main [project README](../README.md) for complete setup instructions.

For backend-only development:

```bash
# 1. Start database
docker compose up postgres -d

# 2. Install dependencies and run
cargo run
```

API runs on `http://localhost:4001` (or address specified by `LISTEN_ADDR`)

## Testing

### Recommended: Docker Test Environment

```bash
# Start test database
docker-compose -f docker-compose.test.yml up -d

# Run all tests
cargo test

# Cleanup
docker-compose -f docker-compose.test.yml down -v
```

### Test Options

```bash
# Specific test modules
cargo test auth_tests
cargo test handlers_tests
cargo test database_tests

# With output
cargo test -- --nocapture

# Verbose mode
cargo test --verbose
```

## Key Features

### Bus Data Scraping
- Scrapes bus information from Runshaw College website
- Implements retry logic with exponential backoff
- **Debug mode**: Serves dummy data when scraping fails
- **Release mode**: Returns empty results when scraping fails

### Caching
- In-memory cache for bus data
- Configurable cache duration via `CACHE_DURATION_MINUTES`
- Reduces load on external services

### Authentication
- JWT-based authentication with secure HTTP-only cookies
- User registration and login
- Role-based access control (User/Admin)
- Password hashing with bcrypt

### Database
- PostgreSQL with async SQLx ORM
- Automatic migrations on startup
- Connection pooling for performance

## Environment Variables

All environment variables should be set in the project root `.env` file:

```bash
# Database connection
DATABASE_URL=postgresql://user:password@localhost:5432/businfo

# Server configuration
LISTEN_ADDR=0.0.0.0:4001
RUST_LOG=info

# JWT authentication
JWT_SECRET_KEY=your_secret_key_here

# Bus data caching
CACHE_DURATION_MINUTES=5
```

## API Endpoints

| Method | Endpoint             | Description             |
| ------ | -------------------- | ----------------------- |
| `GET`  | `/api/health`        | Simple health check     |
| `GET`  | `/api/health/status` | Detailed health status  |
| `GET`  | `/api/buses/current` | Current bus information |
| `POST` | `/api/auth/login`    | User login              |
| `POST` | `/api/auth/register` | User registration       |
| `POST` | `/api/auth/logout`   | User logout             |
| `GET`  | `/api/auth/me`       | Current user info       |

## Dependencies

- **axum** - Web framework for Rust
- **sqlx** - Async PostgreSQL driver and ORM
- **tokio** - Async runtime
- **serde** - Serialization/deserialization
- **jsonwebtoken** - JWT token handling
- **bcrypt** - Password hashing
- **anyhow** - Error handling
- **tracing** - Better logging
- **reqwest** - HTTP client for scraping
- **scraper** - HTML parsing

## Database Schema

Migrations are located in `migrations/` directory

## Development Notes

- The scraper targets `https://webservices.runshaw.ac.uk/bus/busdepartures.aspx`
- HTML parsing uses CSS selectors to extract bus and bay information
- In debug builds, dummy data is generated following the pattern: T1, T2, A1, B1, C1, A2, B2, etc.
- Some buses are marked as "not arrived" (bay: null) in dummy data
