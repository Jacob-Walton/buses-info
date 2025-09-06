#!/bin/sh
# cleanup.sh
# Cleans up project setup done by bootstrap.sh

set -eu

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$REPO_ROOT"

info() { printf "\033[1;34m[INFO]\033[0m %s\n" "$1"; }
error() { printf "\033[1;31m[ERROR]\033[0m %s\n" "$1" >&2; }

check_command() {
    command -v "$1" >/dev/null 2>&1 || {
        error "Required command '$1' not found. Please install it first."
        exit 1
    }
}

info "Checking required tools..."
check_command git
check_command npm
check_command cargo

info "Resetting Git hooks path to default (.git/hooks)..."
git config --unset core.hooksPath || true

info "Cleaning Node dependencies..."
(
    cd frontend || { error "Missing frontend directory"; exit 1; }
    rm -rf node_modules pnpm-lock.yaml .next
)

info "Cleaning Rust build artifacts..."
(
    cd backend || { error "Missing backend directory"; exit 1; }
    cargo clean
)

info "Cleanup complete. Project is back to a fresh state."
