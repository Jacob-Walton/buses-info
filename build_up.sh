#!/bin/bash

# Usage: ./build_up.sh [component] [use_cache]

component=""
cache="true"

for arg in "$@"; do
    case "$arg" in
        true|false)
            cache="$arg"
            ;;
        *)
            component="$arg"
            ;;
    esac
done

build_command=(docker compose build)
[ "$cache" = "false" ] && build_command+=(--no-cache)
[ -n "$component" ] && build_command+=("$component")

echo "Running command: ${build_command[*]}"
"${build_command[@]}"

result="$?"
if [ $result != "0" ]; then
    echo "Command exited with $result"
else
    echo "Command exited successfully"
fi

up_command=(docker compose up -d)
[ -n "$component" ] && build_command+=("$component")

echo "Running command: ${up_command[*]}"
"${up_command[@]}"

result="$?"
if [ $result != "0" ]; then
    echo "Command exited with $result"
else
    echo "Command exited successfully"
fi