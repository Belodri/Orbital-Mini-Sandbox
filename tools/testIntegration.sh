#!/bin/bash

# --- Integration Test Runner for Orbital Mini-Sandbox ---
# This script builds the app, serves it locally, runs the Playwright tests,
# and then cleans up by stopping the server.

# Exit immediately if a command exits with a non-zero status.
set -e

# --- Configuration ---
DIST_PATH="dist"
TEST_PROJECT_PATH="Tests/IntegrationTests/IntegrationTests.csproj"
TEST_RUNSETTINGS_PATH="Tests/IntegrationTests/IntegrationTests.runsettings"
PORT=8080 # The port our tests will connect to
SERVER_URL="http://localhost:$PORT"
WAIT_TIMEOUT_SECONDS=30 # Max time to wait for the server to start

# --- 1. Build the application ---
echo "Building the application..."
./tools/build.sh

# --- 2. Start a static web server in the background ---
echo "Starting web server for '$DIST_PATH' on port $PORT..."
# 'npx http-server' runs a local server without a global install.
# We run it in the background (&) and save its Process ID (PID).
npx http-server "$DIST_PATH" -p $PORT --silent &
SERVER_PID=$!

# --- 3. Run the tests ---
# We use a 'trap' to ensure the server is killed when the script exits,
# for any reason (success, failure, or Ctrl+C). This is crucial for cleanup.
trap 'echo "Shutting down web server..."; npx kill-port $PORT' EXIT

echo "Waiting for server to become available at $SERVER_URL (timeout in ${WAIT_TIMEOUT_SECONDS}s)..."

attempts=0

while ! curl -s --head --fail "$SERVER_URL" > /dev/null; do

    if [ $attempts -ge $WAIT_TIMEOUT_SECONDS ]; then
        echo
        echo "Error: Timed out after ${WAIT_TIMEOUT_SECONDS} seconds waiting for server to start."
        exit 1
    fi

    echo -n "."
    attempts=$((attempts + 1))
    sleep 1
done

echo
echo "Server is up and running!"

echo "Running Playwright integration tests against local server..."
# Run the tests. The BaseUrl in the C# test code must match the server port.
dotnet test "$TEST_PROJECT_PATH" --settings "$TEST_RUNSETTINGS_PATH" -- TestRunParameters.Parameter\(name=\"BaseUrl\", value=\"$SERVER_URL\"\)

# The 'trap' command will execute on exit, so we don't need a final kill command.
echo "--- Test run finished successfully! ---"
