#!/bin/bash

set -e

# Force Git Bash/MSYS2 to use true Windows symlinks ('nativestrict').
# This requires either Administrator rights or Developer Mode to be enabled on Windows.
export MSYS=winsymlinks:nativestrict

TYPE_GEN_PROJECT_FILE="tools/TsTypeGen/TsTypeGen.csproj"
BRIDGE_PATH="src/Bridge"
BRIDGE_BUILD_TYPES_COMMAND="npm run build:types"
SOURCE_TYPES_DIR="src/Bridge/types"
DEST_TYPES_DIR="src/WebApp/types"

echo "Build and symlink TypeScript types..."

# Run the dotnet tool to generate types.
dotnet run --project "$TYPE_GEN_PROJECT_FILE"

# Build the types from Bridge.
(cd "$BRIDGE_PATH" && $BRIDGE_BUILD_TYPES_COMMAND)

# Check if the destination directory exists; if not create it.
mkdir -p "$DEST_TYPES_DIR"

# Symlink all files from the source directory into the target directory, overriding any existing ones.
for source_file in "$SOURCE_TYPES_DIR"/*; do
    # Check to prevent an error if the source directory is empty.
    if [ -e "$source_file" ]; then
        ln -sfr "$source_file" "$DEST_TYPES_DIR"
    fi
done

echo "Build and symlink TypeScript types complete."