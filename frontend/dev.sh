#!/bin/bash

# Development server startup script with backend proxy
# Usage: ./dev-with-proxy.sh [backend_url]

BACKEND_URL=${1:-"http://localhost:4001"}

echo "Starting frontend development server..."
echo "Backend proxy URL: $BACKEND_URL"
echo ""
echo "The frontend will proxy /api/* requests to: $BACKEND_URL/api/*"
echo "Press Ctrl+C to stop the development server"
echo ""

BACKEND_URL=$BACKEND_URL npm run dev
