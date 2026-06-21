#!/usr/bin/env bash
#
# Loads credentials from a .env file and launches the Unity game server.
#
# The Unity server (ServerManager.LoadApiCredentials) reads API_USERNAME / API_PASSWORD
# from OS environment variables. This wrapper reads them from a .env file so they are
# never baked into the build and are kept out of git.
#
# Usage:
#   ./run-server.sh [extra Unity args...]
#
# Config (override via environment if needed):
#   ENV_FILE    Path to the .env file        (default: <script dir>/.env)
#   SERVER_BIN  Path to the server binary     (default: auto-detected in <script dir>)
#
# Examples:
#   ./run-server.sh
#   SERVER_BIN=./CardArchiveServer.x86_64 ./run-server.sh -logFile server.log

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"

# --- Load .env -------------------------------------------------------------
if [ ! -f "$ENV_FILE" ]; then
    echo "ERROR: env file not found: $ENV_FILE" >&2
    echo "       Copy .env.example to .env and fill in the values." >&2
    exit 1
fi

while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"                 # strip trailing CR (Windows-edited files)
    case "$line" in
        ''|\#*) continue ;;              # skip blank lines and comments
    esac
    line="${line#export }"               # allow an optional "export " prefix
    key="${line%%=*}"
    val="${line#*=}"
    key="$(printf '%s' "$key" | tr -d '[:space:]')"
    [ -z "$key" ] && continue
    case "$val" in                       # strip one layer of surrounding quotes
        \"*\") val="${val#\"}"; val="${val%\"}" ;;
        \'*\') val="${val#\'}"; val="${val%\'}" ;;
    esac
    export "$key=$val"
done < "$ENV_FILE"

# --- Validate --------------------------------------------------------------
if [ -z "${API_USERNAME:-}" ] || [ -z "${API_PASSWORD:-}" ]; then
    echo "ERROR: API_USERNAME and/or API_PASSWORD are empty in $ENV_FILE" >&2
    exit 1
fi

# --- Resolve server binary -------------------------------------------------
if [ -z "${SERVER_BIN:-}" ]; then
    SERVER_BIN="$(find "$SCRIPT_DIR" -maxdepth 1 -type f \( -name '*.x86_64' -o -name '*.x86' \) 2>/dev/null | head -n 1 || true)"
fi
if [ -z "${SERVER_BIN:-}" ] || [ ! -f "$SERVER_BIN" ]; then
    echo "ERROR: server binary not found. Set SERVER_BIN to the Unity server executable." >&2
    exit 1
fi
chmod +x "$SERVER_BIN" 2>/dev/null || true

# --- Launch (headless) -----------------------------------------------------
echo "Launching $SERVER_BIN as user '$API_USERNAME'..."
exec "$SERVER_BIN" -batchmode -nographics "$@"
