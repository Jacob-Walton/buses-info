# Frontend (Next.js)

React frontend built with Next.js and TypeScript.

## Quick Start

See the main [project README](../README.md) for complete setup instructions.

For frontend-only development:

```bash
npm install
npm run dev
```

Runs on `http://localhost:3000`

## Development with Backend Proxy

To connect to a local backend during development:

```bash
# Create local environment file
echo "BACKEND_URL=http://localhost:4001" > .env.local

# Start development server
npm run dev
```

### How the Proxy Works

When `BACKEND_URL` is set in development:
- All `/api/*` requests are automatically proxied to `${BACKEND_URL}/api/*`
- No CORS configuration needed
- Seamless integration with local backend

**Example proxy behavior:**
- `fetch('/api/auth/login')` → proxies to → `http://localhost:8080/api/auth/login`
- `fetch('/api/buses/current')` → proxies to → `http://localhost:8080/api/buses/current`

## Available Scripts

```bash
npm run dev      # Start development server
npm run build    # Build for production
npm run start    # Start production server
npm run lint     # Run ESLint
```

## Environment Variables

### Development (.env.local)

```bash
# Backend proxy (development only)
BACKEND_URL=http://localhost:4001

# Public API URL (production)
NEXT_PUBLIC_API_URL=/api
```

### Convenience Scripts

Use the provided script for easy development:

```bash
# Start with backend proxy
./dev.sh

# Or with custom backend URL
./dev.sh http://localhost:3001
```

## Architecture

- **Framework**: Next.js 15 with App Router
- **Language**: TypeScript
- **Styling**: SCSS modules
- **State Management**: React Context for authentication
- **HTTP Client**: Native `fetch()` API
- **Build Tool**: Next.js built-in bundler

### Key Features

- **Authentication**: JWT-based with HTTP-only cookies
- **Bus Information**: Real-time bus arrival data
- **Responsive Design**: Mobile-first approach
- **Type Safety**: Full TypeScript coverage
- **Development Proxy**: Automatic API proxying in development

### Project Structure

```
src/
├── app/                 # Next.js App Router pages
├── components/          # Reusable UI components
│   ├── common/         # Shared components
│   ├── features/       # Feature-specific components
│   ├── layout/         # Layout components
│   └── ui/             # Basic UI elements
├── hooks/              # Custom React hooks
├── lib/                # Utility functions
├── providers/          # React context providers
├── styles/             # Global styles and SCSS
└── types/              # TypeScript type definitions
```