#!/bin/bash

# --- Build project and start HTTP server ---
# 
# Usage:
#   ./testserver.sh         - Builds the project in Debug mode (default).
#   ./testserver.sh --release    - Builds the project in Release mode.


# --- Argument Parsing ---
# Default build script flag is --debug
BUILD_SCRIPT_FLAG="--debug"

# Check the first argument for a release flag.
if [[ "$1" == "--release" ]]; then
  BUILD_SCRIPT_FLAG="--release"
fi


# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Building project..."
# Run the build script
"$SCRIPT_DIR/build.sh" "$BUILD_SCRIPT_FLAG" 

# Check if build was successful
if [ $? -ne 0 ]; then
    echo "Build failed. Aborting server start."
    exit 1
fi

echo "Build completed successfully."
echo "Starting HTTP server in dist/ directory..."

# Start server from project root serving the dist directory
cd "$PROJECT_ROOT"
http-server dist/