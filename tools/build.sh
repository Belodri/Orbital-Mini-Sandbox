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

# The path where the dotnet build process places its output.
# This is inside the C# project's bin directory.
BRIDGE_BUILD_OUTPUT_PATH="src/Bridge/bin/Release/net9.0/wwwroot"

# --- 1. Clean: Remove the old distribution folder ---
echo "Cleaning old distribution folder..."
rm -rf "$DIST_PATH"
mkdir -p "$DIST_PATH"

# --- 2. Build: Compile the C# Bridge project ---
echo "Building C# EngineBridge in Release mode..."
# The 'dotnet publish' command is ideal for this. It builds the project
# and places all necessary files for deployment in a standard location.
# We specify the output directory to be the default one for clarity.
dotnet publish "$BRIDGE_PROJECT_PATH" -c Release

# --- 3. Deploy: Copy assets to the distribution folder ---
echo "Copying web assets to '$DIST_PATH'..."


# 3a. Copy the entire framework output (_framework folder)
# This contains the .NET runtime, our DLLs, and the dotnet.js loader.
echo "Copying framework..."
cp -r "$BRIDGE_BUILD_OUTPUT_PATH/_framework" "$DIST_PATH/"

# 3b. Copy the WebApp's HTML and JavaScript files.
# NOTE: Replace this with a proper build process later
echo "Copying WebApp files (HTML, JS)..."
cp -r "$WEBAPP_SOURCE_PATH/"* "$DIST_PATH/"

# --- Finished ---
echo "--- Build successful! ---"
echo "Application is ready in the '$DIST_PATH' folder."
echo "You can now serve this folder with a static web server."