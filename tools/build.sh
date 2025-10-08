##!/usr/bin/env bash

# Usage:
#   ./build.sh          - Builds the project in Release mode (default).
#   ./build.sh --debug  - Builds the project in Debug mode.

# Exit immediately if a command exits with a non-zero status.
set -e

# --- Argument Parsing ---
# Default build configuration is Release
BUILD_CONFIG="Release"
VITE_MODE="production"

# Check the first argument for a debug flag.
if [[ "$1" == "--debug" ]]; then
  BUILD_CONFIG="Debug"
  VITE_MODE="debug"
fi


# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"


# --- CONFIGURATION ---
# All paths are relative to the project root.

# Source locations
BRIDGE_PROJECT_PATH="src/Bridge/Bridge.csproj"
FRAMEWORK_SRC_DIR="src/Bridge/bin/$BUILD_CONFIG/net9.0/publish/wwwroot/_framework"

# Target locations
DIST_DIR="dist"
FRAMEWORK_DEST_DIR="$DIST_DIR/_framework"

# 1. Clean the dist directory
echo "Cleaning dist directory..."
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"

# 2. Compile C# code and build WASM
echo "Building .NET and WASM artifacts..."
dotnet publish "$BRIDGE_PROJECT_PATH" -c "$BUILD_CONFIG"

# 3. Copy required build artifacts into dist
echo "Copying WASM framework to dist..."
# The -R (or -r) flag copies directories recursively.
cp -r "$FRAMEWORK_SRC_DIR" "$FRAMEWORK_DEST_DIR"

# 4. Compile & bundle the TypeScript/WebApp code
echo "Building WebApp with Vite..."
# Vite places the output directly into the 'dist' directory
npx vite build --mode "$VITE_MODE"
