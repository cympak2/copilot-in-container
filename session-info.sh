#!/bin/bash
# Helper script to display copilot-in-container session information
# This script is installed in the Docker image and can be run as: session-info

if [ -z "$COPILOT_IN_CONTAINER_SESSION_INFO" ]; then
  echo "⚠️  No session information available"
  echo "This environment variable is only set when running in COPILOT_IN_CONTAINER containers"
  exit 1
fi

# Check if jq is available for pretty printing
if command -v jq >/dev/null 2>&1; then
  echo "$COPILOT_IN_CONTAINER_SESSION_INFO" | jq .
else
  # Fallback: Use Python for pretty-printing (Node.js containers have Python)
  if command -v python3 >/dev/null 2>&1; then
    echo "$COPILOT_IN_CONTAINER_SESSION_INFO" | python3 -m json.tool
  elif command -v python >/dev/null 2>&1; then
    echo "$COPILOT_IN_CONTAINER_SESSION_INFO" | python -m json.tool
  else
    # Last resort: plain JSON with manual formatting attempt
    echo "Session Information (install jq for better formatting):"
    echo "$COPILOT_IN_CONTAINER_SESSION_INFO" | sed 's/,/,\n/g'
  fi
fi
