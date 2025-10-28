#!/bin/bash

# Partnership Agent - Azure Live Test Setup Script
# This script configures Azure SQL and Azure AI Search for testing the Agent Framework v2

set -e

echo "==================================================================="
echo "Partnership Agent - Azure Live Test Setup"
echo "==================================================================="
echo ""
echo "This script will help you configure and test the Agent Framework v2"
echo "agents with Azure SQL and Azure AI Search."
echo ""

# Function to prompt for input with a default value
prompt_with_default() {
    local prompt="$1"
    local default="$2"
    local value

    if [ -n "$default" ]; then
        read -p "$prompt [$default]: " value
        echo "${value:-$default}"
    else
        read -p "$prompt: " value
        echo "$value"
    fi
}

# Check if running with existing environment variables
if [ -n "$AZURE_OPENAI_ENDPOINT" ]; then
    echo "✓ Found existing Azure OpenAI configuration"
    USE_EXISTING=$(prompt_with_default "Use existing environment variables? (y/n)" "y")
    if [ "$USE_EXISTING" != "y" ]; then
        unset AZURE_OPENAI_ENDPOINT
        unset AZURE_OPENAI_API_KEY
        unset AZURE_OPENAI_DEPLOYMENT_NAME
        unset AZURE_SEARCH_ENDPOINT
        unset AZURE_SEARCH_API_KEY
        unset AZURESQL_CONNECTION_STRING
    fi
fi

# Azure OpenAI Configuration
echo ""
echo "==================================================================="
echo "Azure OpenAI Configuration"
echo "==================================================================="

if [ -z "$AZURE_OPENAI_ENDPOINT" ]; then
    AZURE_OPENAI_ENDPOINT=$(prompt_with_default "Azure OpenAI Endpoint (e.g., https://your-resource.openai.azure.com)" "")
    AZURE_OPENAI_API_KEY=$(prompt_with_default "Azure OpenAI API Key" "")
    AZURE_OPENAI_DEPLOYMENT_NAME=$(prompt_with_default "Chat Deployment Name" "gpt-4o")
    AZURE_OPENAI_EMBEDDING_DEPLOYMENT=$(prompt_with_default "Embedding Deployment Name" "text-embedding-ada-002")
else
    echo "Endpoint: $AZURE_OPENAI_ENDPOINT"
    echo "Deployment: $AZURE_OPENAI_DEPLOYMENT_NAME"
fi

# Azure AI Search Configuration
echo ""
echo "==================================================================="
echo "Azure AI Search Configuration"
echo "==================================================================="

if [ -z "$AZURE_SEARCH_ENDPOINT" ]; then
    AZURE_SEARCH_SERVICE_NAME=$(prompt_with_default "Azure AI Search Service Name (just the name, not URL)" "")
    AZURE_SEARCH_ENDPOINT="https://${AZURE_SEARCH_SERVICE_NAME}.search.windows.net"
    AZURE_SEARCH_API_KEY=$(prompt_with_default "Azure AI Search API Key" "")
else
    echo "Endpoint: $AZURE_SEARCH_ENDPOINT"
fi

# Azure SQL Configuration
echo ""
echo "==================================================================="
echo "Azure SQL Configuration"
echo "==================================================================="

if [ -z "$AZURESQL_CONNECTION_STRING" ]; then
    echo "Azure SQL Connection String format:"
    echo "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=your-db;Persist Security Info=False;User ID=your-user;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    echo ""
    AZURESQL_CONNECTION_STRING=$(prompt_with_default "Azure SQL Connection String" "")
else
    echo "Connection String: ${AZURESQL_CONNECTION_STRING:0:50}..."
fi

# Export environment variables
export AZURE_OPENAI_ENDPOINT
export AZURE_OPENAI_API_KEY
export AZURE_OPENAI_DEPLOYMENT_NAME
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME="${AZURE_OPENAI_EMBEDDING_DEPLOYMENT:-text-embedding-ada-002}"
export AzureSearch__Endpoint="$AZURE_SEARCH_ENDPOINT"
export AzureSearch__ApiKey="$AZURE_SEARCH_API_KEY"
export AzureSearch__UseVectorSearch="true"
export ChatHistory__Provider="AzureSQL"
export AZURESQL_CONNECTION_STRING

echo ""
echo "==================================================================="
echo "Configuration Summary"
echo "==================================================================="
echo "Azure OpenAI: $AZURE_OPENAI_ENDPOINT"
echo "Chat Deployment: $AZURE_OPENAI_DEPLOYMENT_NAME"
echo "Embedding Deployment: $AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME"
echo "Azure Search: $AZURE_SEARCH_ENDPOINT"
echo "Chat History: Azure SQL"
echo ""

# Save configuration to .env file for future use
ENV_FILE="$(dirname "$0")/../.env.azure-test"
cat > "$ENV_FILE" << EOF
# Azure Live Test Configuration
# Generated: $(date)

export AZURE_OPENAI_ENDPOINT="$AZURE_OPENAI_ENDPOINT"
export AZURE_OPENAI_API_KEY="$AZURE_OPENAI_API_KEY"
export AZURE_OPENAI_DEPLOYMENT_NAME="$AZURE_OPENAI_DEPLOYMENT_NAME"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME="$AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME"
export AzureSearch__Endpoint="$AZURE_SEARCH_ENDPOINT"
export AzureSearch__ApiKey="$AZURE_SEARCH_API_KEY"
export AzureSearch__UseVectorSearch="true"
export ChatHistory__Provider="AzureSQL"
export AZURESQL_CONNECTION_STRING="$AZURESQL_CONNECTION_STRING"
EOF

echo "✓ Configuration saved to: $ENV_FILE"
echo "  (To reuse: source $ENV_FILE)"
echo ""

# Ask if user wants to continue with setup
CONTINUE=$(prompt_with_default "Start the API and initialize Azure resources? (y/n)" "y")
if [ "$CONTINUE" != "y" ]; then
    echo "Setup cancelled. Configuration saved to $ENV_FILE"
    exit 0
fi

# Build and start the API
echo ""
echo "==================================================================="
echo "Building and Starting API"
echo "==================================================================="

cd "$(dirname "$0")/.."
dotnet build src/PartnershipAgent.WebApi/PartnershipAgent.WebApi.csproj

# Start the API in the background
echo "Starting API on http://localhost:5000..."
dotnet run --project src/PartnershipAgent.WebApi --no-build &
API_PID=$!

# Wait for API to be ready
echo "Waiting for API to start..."
sleep 10

# Check if API is running
if ! curl -s http://localhost:5000/api/chat/health > /dev/null; then
    echo "❌ API failed to start. Check the logs above."
    kill $API_PID 2>/dev/null || true
    exit 1
fi

echo "✓ API started successfully (PID: $API_PID)"
echo ""

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "Shutting down API..."
    kill $API_PID 2>/dev/null || true
}
trap cleanup EXIT

# Initialize Azure AI Search
echo "==================================================================="
echo "Initializing Azure AI Search"
echo "==================================================================="
echo "Creating index and indexing sample partnership documents..."
echo ""

INIT_RESPONSE=$(curl -s -X POST http://localhost:5000/api/admin/initialize-vector-search)
echo "$INIT_RESPONSE" | jq '.' || echo "$INIT_RESPONSE"
echo ""

# Test the configuration
echo "==================================================================="
echo "Testing Agent Framework v2 with Live Azure Resources"
echo "==================================================================="
echo ""

TEST_QUERY="What are the partnership revenue sharing tiers?"
echo "Test Query: $TEST_QUERY"
echo ""
echo "Streaming response:"
echo "-------------------------------------------------------------------"

curl -X POST http://localhost:5000/api/chat/stream \
  -H "Content-Type: application/json" \
  -d "{\"threadId\":\"azure-test-$(date +%s)\",\"prompt\":\"$TEST_QUERY\"}" \
  2>/dev/null | while IFS= read -r line; do
    if [[ $line == data:* ]]; then
        echo "$line" | sed 's/^data: //' | jq -r 'select(.type == "chunk") | .content' 2>/dev/null || true
        echo "$line" | sed 's/^data: //' | jq 'select(.type != "chunk")' 2>/dev/null || true
    fi
done

echo ""
echo "-------------------------------------------------------------------"
echo ""

# Run another test query
echo "Running second test query..."
TEST_QUERY2="Tell me about partnership compliance requirements"
echo "Test Query: $TEST_QUERY2"
echo ""

RESPONSE=$(curl -s -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"threadId\":\"azure-test-$(date +%s)\",\"prompt\":\"$TEST_QUERY2\"}")

echo "$RESPONSE" | jq '.' || echo "$RESPONSE"
echo ""

echo "==================================================================="
echo "Azure Live Test Complete!"
echo "==================================================================="
echo ""
echo "✓ Agent Framework v2 agents are running with:"
echo "  - Azure OpenAI (Chat: $AZURE_OPENAI_DEPLOYMENT_NAME)"
echo "  - Azure AI Search (Vector search enabled)"
echo "  - Azure SQL (Chat history persistence)"
echo ""
echo "The API is still running at http://localhost:5000"
echo "Press Ctrl+C to stop the API and exit."
echo ""
echo "Next steps:"
echo "  1. View chat history in Azure SQL: SELECT * FROM dbo.ChatMessages"
echo "  2. Test more queries via the API"
echo "  3. Monitor Application Insights for telemetry"
echo ""

# Keep the script running
wait $API_PID
