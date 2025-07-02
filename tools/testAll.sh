#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Run Bridge Tests
(cd Tests/BridgeTests && dotnet test)

# Run Physics Tests
(cd Tests/PhysicsTests && dotnet test)

# Run integration tests
"$SCRIPT_DIR/testIntegration.sh"

echo "-------------------------"
echo "--- All tests passed. ---"
echo "-------------------------"