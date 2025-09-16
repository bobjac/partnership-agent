#!/bin/bash
# Cross-platform launcher for Partnership Agent setup
# Works on Linux, macOS, and Windows (with Git Bash/WSL)

set -e

echo "🚀 Partnership Agent Setup Launcher"
echo "=================================="

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

# Check if .NET is available
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is required but not found"
    echo "Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
    exit 1
fi

echo "🔧 Running cross-platform setup tool..."
cd "$SCRIPT_DIR"
dotnet run --project setup.csproj "$@"