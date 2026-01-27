#!/bin/bash
# Build script for copilot-in-container container image

set -e

readonly IMAGE_NAME="copilot-in-container:latest"
readonly GREEN='\033[0;32m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

echo ""
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}  Building copilot-in-container Container Image${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Check if container command exists
if ! command -v container >/dev/null 2>&1; then
    echo "❌ Apple container command not found"
    echo ""
    echo "Please install Apple container from:"
    echo "  https://github.com/apple/container/releases"
    exit 1
fi

# Build the image
echo "🔧 Building image: $IMAGE_NAME"
echo ""

container build -t "$IMAGE_NAME" .

echo ""
echo -e "${GREEN}✅ Build complete!${NC}"
echo ""
echo "Next steps:"
echo "  1. Run: copilot-in-container (or cic)"
echo "  2. Or: cic \"your prompt here\""
echo ""
