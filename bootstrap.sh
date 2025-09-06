#!/bin/sh
# bootstrap.sh

set -eu

# Resolve repo root
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

run_in() {
    dir="$1"
    shift
    (
        cd "$dir" || { error "Missing directory $dir"; exit 1; }
        "$@"
    )
}

info "Checking required tools..."
check_command git
check_command yarn
check_command cargo

info "Setting Git hooks path to .githooks..."
git config core.hooksPath .githooks

info "Ensuring hook scripts are executable..."
chmod +x .githooks/* || true

info "Installing Node dependencies..."
(
    cd frontend || { error "Missing frontend directory"; exit 1; }
    yarn install
)

info "Fetching Rust dependencies..."
(
    cd backend || { error "Missing backend directory"; exit 1; }
    cargo fetch
)

info "Bootstrap complete."
