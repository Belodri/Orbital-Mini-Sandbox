#!/bin/bash

set -e

# Force Git Bash/MSYS2 to use true Windows symlinks ('nativestrict').
# This requires either Administrator rights or Developer Mode to be enabled on Windows.
export MSYS=winsymlinks:nativestrict

PROJECT_FILE="tools/TsTypeGen/TsTypeGen.csproj"
SOURCE_BRIDGE_TYPE_FILE="src/Bridge/types/Bridge.d.ts"
SOURCE_LAYOUT_RECORDS_TYPE_FILE="src/Bridge/types/LayoutRecords.d.ts"
DEST_TYPES_DIR="src/WebApp/types"

echo "Build and symlink TypeScript types..."

# Run the dotnet tool to generate types
dotnet run --project "$PROJECT_FILE"

# Check if the destination directory exists; if not create it.
mkdir -p "$DEST_TYPES_DIR"

# Symlink the two source files into the target directory, overriding any existing ones
ln -sfr "$SOURCE_BRIDGE_TYPE_FILE" "$DEST_TYPES_DIR"
ln -sfr "$SOURCE_LAYOUT_RECORDS_TYPE_FILE" "$DEST_TYPES_DIR"

echo "Build and symlink TypeScript types complete."