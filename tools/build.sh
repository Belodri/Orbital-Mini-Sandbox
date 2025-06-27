#!/bin/bash

# --- Build Script for Orbital Mini-Sandbox ---
# This script is designed to be simple and cross-platform.
# It cleans the distribution folder, builds the C# project,
# and copies the necessary web assets to create a final,
# hostable application in the /dist folder.

# Exit immediately if a command exits with a non-zero status.
set -e

# --- Configuration ---
# Define project paths to make the script easy to read and modify.
# The script is run from the root, so paths are relative to the root.
BRIDGE_PROJECT_PATH="src/Bridge/Bridge.csproj"
WEBAPP_SOURCE_PATH="src/WebApp"
DIST_PATH="dist"
BRIDGE_DIST_PATH="$DIST_PATH/bridge"
SCRIPTS_DIST_PATH="$DIST_PATH/scripts"

# The path where the dotnet build process places its output.
# This is inside the C# project's bin directory.
BRIDGE_BUILD_OUTPUT_PATH="src/Bridge/bin/Release/net9.0/publish/wwwroot"

# The path where the Vite build process places its output.
WEBAPP_BUILD_OUTPUT_PATH="$WEBAPP_SOURCE_PATH/dist"

# --- 1. Clean: Remove the old distribution folder ---
echo "Cleaning old distribution folder..."
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"

# --- 2. Build: Compile the C# Bridge project ---
echo "Building C# EngineBridge in Release mode..."
# 'dotnet publish' builds the project. The actual web assets we need
# will be located in the 'wwwroot' subfolder of the output.
dotnet publish "$BRIDGE_PROJECT_PATH" -c Release

# --- 3. Deploy: Copy bridge assets to the distribution folder ---
echo "Copying bridge assets to '$BRIDGE_DIST_PATH'..."
# We copy ONLY the contents of wwwroot, as 'dotnet publish' creates
# other files that we don't need.
mkdir "$BRIDGE_DIST_PATH"
cp "$BRIDGE_BUILD_OUTPUT_PATH/Bridge.mjs" "$BRIDGE_DIST_PATH/"
cp -r "$BRIDGE_BUILD_OUTPUT_PATH/_framework" "$BRIDGE_DIST_PATH/"

# --- 4. Build WebApp with Vite ---
echo "Building WebApp frontend with Vite..."
# We change into the WebApp directory to run its package.json scripts.
# 'npm install' ensures all dependencies are present.
# 'npm run build' executes the 'vite build' command defined in package.json.
# Running this in a subshell ( ... ) keeps the main script in the root directory.
(cd "$WEBAPP_SOURCE_PATH" && npm install && npm run build)

# --- 5. Deploy WebApp assets ---
echo "Copying WebApp files (HTML, bundled JS, CSS)..."
# We now copy the entire contents of the WebApp's build output.
# This works because the C# assets are in `dist/bridge/` and the
# WebApp assets (index.html, scripts/, styles/) will not conflict.
cp -r "$WEBAPP_BUILD_OUTPUT_PATH/." "$DIST_PATH/"

# --- Finished ---
echo "--- Build successful! ---"
echo "Application is ready in the '$DIST_PATH' folder."
echo "You can now serve this folder with a static web server."