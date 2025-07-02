#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Run Bridge Tests
(cd "$PROJECT_ROOT/Tests/BridgeTests" && dotnet test)

# Run Physics Tests
(cd "$PROJECT_ROOT/Tests/PhysicsTests" && dotnet test)

# Run E2E tests
"$SCRIPT_DIR/testE2E.sh" --chromium --firefox --webkit

echo "-------------------------"
echo "--- All tests passed. ---"
echo "-------------------------"