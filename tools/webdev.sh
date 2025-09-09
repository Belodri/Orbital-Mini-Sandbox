#!/bin/bash

# --- Shortcut to start the vite dev server ---

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
WEBAPP_PATH="src/WebApp"

# Navigate to the WebApp dir and run the dev script
(cd "$PROJECT_ROOT/$WEBAPP_PATH" && npm run dev)