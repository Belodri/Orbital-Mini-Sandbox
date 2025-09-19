#!/bin/bash

# --- Build Script for Orbital Mini-Sandbox ---
# This script cleans the distribution folder, builds the C# project,
# generates all necessary TypeScript type definitions, builds the
# frontend, and copies all assets into the final /dist folder.
#
# Usage:
#   ./build.sh          - Builds the project in Release mode (default).
#   ./build.sh --debug  - Builds the project in Debug mode.


# Exit immediately if a command exits with a non-zero status.
set -e

# --- Argument Parsing ---
# Default build configuration is Release
BUILD_CONFIG="Release"

# Check the first argument for a debug flag.
if [[ "$1" == "--debug" ]]; then
  BUILD_CONFIG="Debug"
fi


# --- Configuration ---
# All paths are relative to the project root where this script is run.
BRIDGE_PROJECT_PATH="src/Bridge/Bridge.csproj"
WEBAPP_SOURCE_PATH="src/WebApp"
DIST_PATH="dist"
BRIDGE_DIST_PATH="$DIST_PATH/bridge"
SCRIPTS_DIST_PATH="$DIST_PATH/scripts"

# Build output locations
BRIDGE_BUILD_OUTPUT_PATH="src/Bridge/bin/$BUILD_CONFIG/net9.0/publish/wwwroot"
WEBAPP_BUILD_OUTPUT_PATH="$WEBAPP_SOURCE_PATH/dist"


# --- Clean: Remove the old distribution folder ---
echo "--- Cleaning old distribution folder... ---"
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"


# --- Build: C# Bridge ---
# Compile the C# Bridge project
echo "--- Building C# EngineBridge in $BUILD_CONFIG mode... ---"
# 'dotnet publish' builds the project. The actual web assets we need
# will be located in the 'wwwroot' subfolder of the output.
dotnet publish "$BRIDGE_PROJECT_PATH" -c "$BUILD_CONFIG"

echo "--- Deploying bridge assets to '$BRIDGE_DIST_PATH'... ---"
# We copy only certain contents of wwwroot, as 'dotnet publish' 
# creates other files that we don't need.
mkdir "$BRIDGE_DIST_PATH"
cp -r "$BRIDGE_BUILD_OUTPUT_PATH/." "$BRIDGE_DIST_PATH/"


# --- Generate and build WebApp --- 
echo "--- Preparing WebApp dependencies and types... ---"
# Generate types from C# and create symlinks for the WebApp.
./tools/buildSymlinkTypes.sh

# Install dependencies, generate types from JSDoc, and build the WebApp
echo "--- Installing dependencies, generating types, and building WebApp... ---"
(
    cd "$WEBAPP_SOURCE_PATH"
    echo "Running npm install in '$WEBAPP_SOURCE_PATH'..."
    npm install
    
    echo "Generating TypeScript declarations from JSDoc..."
    npm run build:types
    
    echo "Building WebApp frontend with Vite..."
    npm run build
)

echo "--- Deploying WebApp assets to '$DIST_PATH'... ---"
# Copy the entire contents of the WebApp's build output.
cp -r "$WEBAPP_BUILD_OUTPUT_PATH/." "$DIST_PATH/"

# --- Finished ---
echo
echo "------------------------------------------------"
echo "--------------- Build successful ---------------"
echo "Application ready in the '$DIST_PATH' directory."
echo "------------------------------------------------"
