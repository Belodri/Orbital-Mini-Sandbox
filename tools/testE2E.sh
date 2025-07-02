#!/bin/bash

# --- E2E Test Runner for Orbital Mini-Sandbox (Playwright-Managed Server) ---
# This script builds the app, sets configuration, and runs Playwright.
# It is position-independent and can be run from any directory.
#
# Usage:
#   ./tools/testE2E.sh [--no-build] [--chromium] [--firefox] [--webkit]
#
# Flags:
#   --no-build   : Skips the build and runs tests on the existing 'dist' content.
#   --chromium   : Run tests on Chromium.
#   --firefox    : Run tests on Firefox.
#   --webkit     : Run tests on WebKit (Safari).
#   If no browser flags are specified, it defaults to running on Chromium.

# Exit immediately if a command exits with a non-zero status.
set -e


# --- Default settings and argument parsing ---
DO_BUILD=true
PROJECTS_TO_RUN=() # Array to hold browser project names

for arg in "$@"
do
    case $arg in
        --no-build)
        DO_BUILD=false
        shift
        ;;
        --chromium)
        PROJECTS_TO_RUN+=("chromium")
        shift
        ;;
        --firefox)
        PROJECTS_TO_RUN+=("firefox")
        shift
        ;;
        --webkit)
        PROJECTS_TO_RUN+=("webkit")
        shift
        ;;
    esac
done

# If no browser flags were provided, default to chromium.
if [ ${#PROJECTS_TO_RUN[@]} -eq 0 ]; then
    echo "No browser specified, defaulting to Chromium."
    PROJECTS_TO_RUN+=("chromium")
fi


# --- Determine absolute paths for script and project root ---
# This makes the script runnable from any location.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"


# --- Configuration using absolute paths ---
DIST_PATH="$PROJECT_ROOT/dist"
E2E_TEST_DIR="$PROJECT_ROOT/Tests/E2ETests"
PORT=8080


# --- Export environment variables for Playwright to use ---
# These are passed to the Playwright config file.
export E2E_PORT=$PORT
export E2E_BASE_URL="http://localhost:$PORT"
export E2E_DIST_PATH="$DIST_PATH"

echo "Configuration:"
echo "  - Project Root: $PROJECT_ROOT"
echo "  - Port: $E2E_PORT"
echo "  - Base URL: $E2E_BASE_URL"
echo "  - App Path: $E2E_DIST_PATH"
echo


# --- Build the application (conditionally) ---
if [ "$DO_BUILD" = true ] ; then
    echo "Building the application..."
    "$PROJECT_ROOT/tools/build.sh"
else
    echo "Skipping application build as requested by --no-build flag."
    if [ ! -d "$DIST_PATH" ]; then
        echo "Error: --no-build was specified, but the '$DIST_PATH' directory does not exist."
        exit 1
    fi
fi


# --- Install Playwright Browsers (if necessary) ---
echo "Checking/installing Playwright dependencies..."
(
    cd "$E2E_TEST_DIR"
    npm install --silent
    npx playwright install --with-deps
)


# --- Run the tests ---
# Build the final list of arguments for Playwright
PLAYWRIGHT_ARGS=()
for project in "${PROJECTS_TO_RUN[@]}"; do
    PLAYWRIGHT_ARGS+=(--project "$project")
done

echo "Running Playwright E2E tests..."
(
    cd "$E2E_TEST_DIR"
    npx playwright test "${PLAYWRIGHT_ARGS[@]}"
)

echo "--- E2E Test run finished successfully! ---"