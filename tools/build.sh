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
BRIDGE_DIST_PATH="dist/bridge"

# The path where the dotnet build process places its output.
# This is inside the C# project's bin directory.
BRIDGE_BUILD_OUTPUT_PATH="src/Bridge/bin/Release/net9.0/publish/wwwroot"

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
# other hosting files (e.g., web.config) that we don't need.
mkdir "$BRIDGE_DIST_PATH"
cp "$BRIDGE_BUILD_OUTPUT_PATH/bridge.mjs" "$BRIDGE_DIST_PATH/"
cp -r "$BRIDGE_BUILD_OUTPUT_PATH/_framework" "$BRIDGE_DIST_PATH/"

# --- 4. Deploy: Copy WebApp assets to the distribution folder ---
# NOTE: Replace this with a proper build process later
# We explicitly copy only the needed files and folders to avoid including
# development-only files like .d.ts, jsconfig.json, etc.
echo "Copying WebApp files (HTML, JS, CSS)..."
cp "$WEBAPP_SOURCE_PATH/index.html" "$DIST_PATH/"
cp -r "$WEBAPP_SOURCE_PATH/scripts" "$DIST_PATH/"
cp -r "$WEBAPP_SOURCE_PATH/styles" "$DIST_PATH/"

# --- Finished ---
echo "--- Build successful! ---"
echo "Application is ready in the '$DIST_PATH' folder."
echo "You can now serve this folder with a static web server."