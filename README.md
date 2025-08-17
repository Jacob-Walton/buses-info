# Bus Info

> [!WARNING]
> This project is currently a work in progress, has not been tested and may not function correctly, it is intended to be a replacement for the old dotnet app one day.

![Rust](https://img.shields.io/badge/Rust-CE412B?style=for-the-badge&logo=rust&logoColor=FFF)
![Next.js](https://img.shields.io/badge/Next.js-000000?style=for-the-badge&logo=nextdotjs&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?style=for-the-badge&logo=postgresql&logoColor=white)

## Overview

This repository contains the source code for my Bus Info project. It provides almost real-time information about bus arrivals at Runshaw College.

## Technical Framework

### Core Technolgoies

- Rust
- Next.js
- PostgreSQL

### System Structure

This application utilises two different technologies for the front and backend, Next.js and Rust.

### Authentication Methods

- Cookie-based web authentication
- API key system (Not Implemented Yet)

## Development Configuration

This project is set up for easy local development using Docker Compose. It brings up everything you need:

- **PostgreSQL** database
- **Rust backend API**
- **Next.js frontend**
- **nginx** reverse proxy

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/)
- [Node.js](https://nodejs.org/) (optional, for local frontend dev)
- [Rust toolchain](https://rustup.rs/) (optional, for local backend dev)

### Environment Variables

Copy the example environment file and fill in your own secrets:

```sh
cp .env.example .env
```

You’ll need to set values for:

- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `DATABASE_URL`
- `LISTEN_ADDR`, `CACHE_DURATION_MINUTES`, `RUST_LOG`

Environment variables are loaded automatically by the backend, whether you run it from the project root or the backend directory.

### Starting Everything

To build and start all services, just run:

```sh
./build_up.sh
```

Or, if you prefer Docker Compose directly:

```sh
docker compose up --build
```

The backend and frontend aren’t exposed directly; visit [http://localhost:8080](http://localhost:8080) to use the app via nginx.

### Local Development

**Frontend:**

```sh
cd frontend
npm install
npm run dev
```
This runs Next.js locally at [http://localhost:3000](http://localhost:3000) (bypassing nginx).

**Backend:**

```sh
cd backend
cargo run
```
This runs the Rust API locally.

**Database:**

PostgreSQL runs in Docker. Database migrations are in `backend/migrations/` and are applied automatically on container startup.