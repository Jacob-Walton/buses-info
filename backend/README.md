# Backend (Rust API)

Rust backend providing REST API for bus arrival data and authentication.

## Prerequisites

- [Rust toolchain](https://rustup.rs/)
- [PostgreSQL](https://www.postgresql.org/)
- [Docker & Docker Compose](https://docs.docker.com/compose/)
- Environment variables (see project root README)

## Quick Start

1. **Install dependencies:**
   ```sh
   cargo fetch
   ```

2. **Set up environment:**
   Configure `.env` file in project root (see main README)

3. **Start database:**
   ```sh
   docker run --rm -e POSTGRES_DB=businfo -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
   ```

4. **Run the backend:**
   ```sh
   cargo run
   ```

API listens on address specified by `LISTEN_ADDR` environment variable.

## Testing

### Quick Test (Recommended)

```sh
# Start test database
docker-compose -f docker-compose.test.yml up -d

# Run tests
cargo test

# Cleanup
docker-compose -f docker-compose.test.yml down -v
```

### Test Options

```sh
# All tests
cargo test

# Specific module
cargo test auth_tests

# With output
cargo test -- --nocapture

# Verbose
cargo test --verbose
```

### Manual Database Setup

If not using Docker Compose:

```sql
CREATE DATABASE test_buses;
CREATE USER test_user WITH PASSWORD 'test_pass';
GRANT ALL PRIVILEGES ON DATABASE test_buses TO test_user;
```

```sh
export DATABASE_URL="postgresql://test_user:test_pass@localhost:5432/test_buses"
export JWT_SECRET_KEY="test_secret_key_for_testing_only"
sqlx migrate run --database-url $DATABASE_URL
cargo test
```

## Key Dependencies

- **axum** - Web framework
- **sqlx** - PostgreSQL async ORM
- **serde** - Serialization
- **jsonwebtoken** - JWT authentication
- **tokio-test** - Async testing utilities

## Database

Migrations are in `migrations/` and apply automatically on startup.

---

See main project README for complete setup instructions.
