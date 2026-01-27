#!/bin/bash
# copilot-in-container (cic) - GitHub Copilot CLI in Apple Container
# Simple wrapper to call the copilot-in-container binary

set -e

# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
BINARY_PATH="$SCRIPT_DIR/app/bin/Release/net10.0/copilot-in-container"

# Check if binary exists
if [ ! -f "$BINARY_PATH" ]; then
    echo "❌ copilot-in-container binary not found" >&2
    echo "" >&2
    echo "Please build the project first:" >&2
    echo "  cd app && dotnet build && cd .." >&2
    exit 1
fi

# Call the binary with all arguments
exec "$BINARY_PATH" "$@"
