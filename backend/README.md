# Backend (Rust API)

This directory contains the Rust backend for the Bus Info project. It provides a REST API for bus arrival data and authentication.

## Prerequisites

- [Rust toolchain](https://rustup.rs/)
- [PostgreSQL](https://www.postgresql.org/) (or use Docker Compose)
- Environment variables (see project root README)

## Running Locally

1. Install dependencies:
   ```sh
   cargo fetch
   ```
2. Set up your `.env` file in the project root (see main README for details).
3. Start the database (if not using Docker Compose):
   ```sh
   # Example using Docker
   docker run --rm -e POSTGRES_DB=businfo -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
   ```
4. Run the backend:
   ```sh
   cargo run
   ```

The API will listen on the address specified by `LISTEN_ADDR` in your environment variables.

## Migrations

Database migrations are in `migrations/` and are applied automatically on container startup.

## Main Dependencies

- [axum](https://github.com/tokio-rs/axum) (web framework)
- [sqlx](https://github.com/launchbadge/sqlx) (PostgreSQL async ORM)
- [serde](https://serde.rs/) (serialization)
- [jsonwebtoken](https://github.com/Keats/jsonwebtoken) (JWT auth)

---

For more details, see the main project README.
