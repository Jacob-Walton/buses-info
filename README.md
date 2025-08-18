# Bus Info

> [!WARNING]
> This project is currently a work in progress, has not been tested and may not function correctly, it is intended to be a replacement for the old dotnet app one day.

![Rust](https://img.shields.io/badge/Rust-CE412B?style=for-the-badge&logo=rust&logoColor=FFF)
![Next.js](https://img.shields.io/badge/Next.js-000000?style=for-the-badge&logo=nextdotjs&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?style=for-the-badge&logo=postgresql&logoColor=white)

Bus arrival information for Runshaw College.

**Tech Stack**: Rust backend, Next.js frontend, PostgreSQL database

## Quick Start

```bash
# 1. Setup environment
cp .env.example .env
# Edit .env with your database credentials

# 2. Start everything
./build_up.sh
```

**Access**: [http://localhost:10000](http://localhost:10000)

## Development

### Full Stack

```bash
# Database
docker compose up postgres -d

# Backend (terminal 1)
cd backend && cargo run

# Frontend (terminal 2) 
cd frontend
echo "BACKEND_URL=http://localhost:4001" > .env.local
npm install && npm run dev
```

Frontend: [http://localhost:3000](http://localhost:3000)  
Backend: [http://localhost:4001](http://localhost:4001)

### Frontend Only

```bash
cd frontend && npm run dev
```

## Testing

```bash
# Backend
cd backend
docker-compose -f docker-compose.test.yml up -d
cargo test
docker-compose -f docker-compose.test.yml down -v

# Frontend
cd frontend && npm run lint
```