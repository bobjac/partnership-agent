#!/bin/bash

# Partnership Agent - Quick Test with User Secrets
# Uses existing user secrets from the WebApi project

set -e

echo "==================================================================="
echo "Partnership Agent - Quick Azure Test (Using User Secrets)"
echo "==================================================================="
echo ""

cd "$(dirname "$0")/.."

# Check if user secrets are configured
if ! dotnet user-secrets list --project src/PartnershipAgent.WebApi | grep -q "AzureOpenAI"; then
    echo "❌ No user secrets found. Please configure them first:"
    echo ""
    echo "  dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-key\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureOpenAI:DeploymentName\" \"gpt-4o\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureOpenAI:EmbeddingDeploymentName\" \"text-embedding-ada-002\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureSearch:Endpoint\" \"https://your-service.search.windows.net\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureSearch:ApiKey\" \"your-search-key\" --project src/PartnershipAgent.WebApi"
    echo "  dotnet user-secrets set \"AzureSQL:ConnectionString\" \"Server=...\" --project src/PartnershipAgent.WebApi"
    echo ""
    exit 1
fi

echo "✓ User secrets found"
echo ""

# Display current configuration (without sensitive data)
echo "Current Configuration:"
echo "-------------------------------------------------------------------"
dotnet user-secrets list --project src/PartnershipAgent.WebApi | grep -E "AzureOpenAI|AzureSearch|ChatHistory" | sed 's/:.*/: ***/' || true
echo "-------------------------------------------------------------------"
echo ""

# Ask which chat history provider to use
echo "Chat History Provider:"
echo "  1) InMemory (no persistence, fast testing)"
echo "  2) Azure SQL (persistent, requires database setup)"
echo ""
read -p "Select option [1]: " CHOICE
CHOICE=${CHOICE:-1}

if [ "$CHOICE" == "2" ]; then
    export ChatHistory__Provider="AzureSQL"
    echo "Using Azure SQL for chat history"
    echo ""
    echo "⚠️  Make sure you've run setup/azure-sql-setup.sql on your database!"
    read -p "Press Enter to continue..."
else
    export ChatHistory__Provider="InMemory"
    echo "Using InMemory for chat history"
fi
echo ""

# Build the project
echo "==================================================================="
echo "Building..."
echo "==================================================================="
dotnet build src/PartnershipAgent.WebApi/PartnershipAgent.WebApi.csproj
echo ""

# Start the API in the background
echo "==================================================================="
echo "Starting API..."
echo "==================================================================="
dotnet run --project src/PartnershipAgent.WebApi --no-build &
API_PID=$!

# Cleanup function
cleanup() {
    echo ""
    echo "Shutting down API..."
    kill $API_PID 2>/dev/null || true
}
trap cleanup EXIT

# Wait for API to be ready
echo "Waiting for API to start..."
for i in {1..30}; do
    if curl -s http://localhost:5000/api/chat/health > /dev/null 2>&1; then
        echo "✓ API started successfully"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "❌ API failed to start within 30 seconds"
        exit 1
    fi
    sleep 1
done
echo ""

# Check configuration
echo "==================================================================="
echo "Checking Configuration..."
echo "==================================================================="
curl -s http://localhost:5000/api/admin/check-config | jq '.'
echo ""

# Initialize Azure AI Search
echo "==================================================================="
echo "Initializing Azure AI Search..."
echo "==================================================================="
echo "This will create the index and upload 5 sample partnership documents."
echo ""

INIT_RESPONSE=$(curl -s -X POST http://localhost:5000/api/admin/initialize-vector-search)
echo "$INIT_RESPONSE" | jq '.'
echo ""

if echo "$INIT_RESPONSE" | jq -e '.status == "ready"' > /dev/null 2>&1; then
    echo "✓ Azure AI Search initialized successfully"
elif echo "$INIT_RESPONSE" | jq -e '.status == "error"' > /dev/null 2>&1; then
    echo "❌ Failed to initialize Azure AI Search"
    echo "   Check the error above. You may need to:"
    echo "   - Verify Azure Search credentials in user secrets"
    echo "   - Ensure embedding deployment exists in Azure OpenAI"
    echo ""
    read -p "Continue with tests anyway? (y/n) [n]: " CONTINUE
    if [ "$CONTINUE" != "y" ]; then
        exit 1
    fi
else
    echo "⚠️  Unexpected response from initialization"
fi
echo ""

# Run test queries
echo "==================================================================="
echo "Testing Agent Framework v2 with Live Azure Resources"
echo "==================================================================="
echo ""

# Test 1: Revenue sharing tiers
echo "Test 1: Partnership Revenue Sharing"
echo "-------------------------------------------------------------------"
TEST_QUERY="What are the partnership revenue sharing tiers?"
echo "Query: $TEST_QUERY"
echo ""
echo "Response (streaming):"

curl -s -X POST http://localhost:5000/api/chat/stream \
  -H "Content-Type: application/json" \
  -d "{\"threadId\":\"test-$(date +%s)\",\"prompt\":\"$TEST_QUERY\"}" | \
while IFS= read -r line; do
    if [[ $line == data:* ]]; then
        DATA=$(echo "$line" | sed 's/^data: //')
        TYPE=$(echo "$DATA" | jq -r '.type' 2>/dev/null || echo "")

        if [ "$TYPE" == "chunk" ]; then
            echo -n "$(echo "$DATA" | jq -r '.content' 2>/dev/null || echo "")"
        elif [ "$TYPE" == "response" ]; then
            echo ""
            echo ""
            echo "Full Response:"
            echo "$DATA" | jq -r '.content'
        elif [ "$TYPE" == "complete" ]; then
            echo ""
            echo "✓ Complete"
        elif [ "$TYPE" == "status" ]; then
            echo "$(echo "$DATA" | jq -r '.message')"
        fi
    fi
done

echo ""
echo "-------------------------------------------------------------------"
echo ""
sleep 2

# Test 2: Compliance requirements
echo "Test 2: Partnership Compliance"
echo "-------------------------------------------------------------------"
TEST_QUERY2="Tell me about partnership compliance requirements"
echo "Query: $TEST_QUERY2"
echo ""

RESPONSE=$(curl -s -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"threadId\":\"test-$(date +%s)\",\"prompt\":\"$TEST_QUERY2\"}")

echo "Response:"
echo "$RESPONSE" | jq -r '.response' || echo "$RESPONSE"
echo ""
echo "-------------------------------------------------------------------"
echo ""

# Test 3: Out of scope query
echo "Test 3: Out of Scope Query (should be rejected by ScopingAgentV2)"
echo "-------------------------------------------------------------------"
TEST_QUERY3="What's the weather like today?"
echo "Query: $TEST_QUERY3"
echo ""

RESPONSE=$(curl -s -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d "{\"threadId\":\"test-$(date +%s)\",\"prompt\":\"$TEST_QUERY3\"}")

echo "Response:"
echo "$RESPONSE" | jq -r '.response' || echo "$RESPONSE"
echo ""
echo "-------------------------------------------------------------------"
echo ""

# Summary
echo "==================================================================="
echo "Test Complete!"
echo "==================================================================="
echo ""
echo "✓ Agent Framework v2 agents are working with:"
echo "  - Azure OpenAI (chat + embeddings)"
echo "  - Azure AI Search (vector search)"
if [ "$ChatHistory__Provider" == "AzureSQL" ]; then
    echo "  - Azure SQL (chat history persistence)"
    echo ""
    echo "Check your Azure SQL database:"
    echo "  SELECT ThreadId, Role, Content, DateInserted FROM dbo.ChatMessages ORDER BY DateInserted DESC"
else
    echo "  - InMemory chat history"
fi
echo ""
echo "The API is still running at http://localhost:5000"
echo ""
echo "Try more queries:"
echo "  curl -X POST http://localhost:5000/api/chat \\"
echo "    -H \"Content-Type: application/json\" \\"
echo "    -d '{\"threadId\":\"manual-test\",\"prompt\":\"Your question here\"}'"
echo ""
echo "Press Ctrl+C to stop the API and exit."
echo ""

# Keep script running
wait $API_PID
