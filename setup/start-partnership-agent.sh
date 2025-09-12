#!/bin/bash

set -e

echo "🚀 Partnership Agent Complete Setup Script"
echo "========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${BLUE}Project root: $PROJECT_ROOT${NC}"

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check prerequisites
echo -e "\n${YELLOW}Checking prerequisites...${NC}"

if ! command_exists docker; then
    echo -e "${RED}Error: Docker is not installed or not in PATH${NC}"
    exit 1
fi

if ! command_exists dotnet; then
    echo -e "${RED}Error: .NET SDK is not installed or not in PATH${NC}"
    exit 1
fi

if ! command_exists curl; then
    echo -e "${RED}Error: curl is not installed or not in PATH${NC}"
    exit 1
fi

echo -e "${GREEN}✓ All prerequisites found${NC}"

# Step 1: Stop existing containers
echo -e "\n${YELLOW}Step 1: Cleaning up existing Elasticsearch containers...${NC}"
docker stop elasticsearch-secure 2>/dev/null || true
docker rm elasticsearch-secure 2>/dev/null || true
docker stop elasticsearch 2>/dev/null || true
docker rm elasticsearch 2>/dev/null || true
docker stop es-local-dev 2>/dev/null || true
docker rm es-local-dev 2>/dev/null || true

# Step 2: Start Elasticsearch with security
echo -e "\n${YELLOW}Step 2: Starting Elasticsearch with security enabled...${NC}"
docker run -d --name elasticsearch-secure \
  -p 9200:9200 -p 9300:9300 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=true" \
  -e "ELASTIC_PASSWORD=changeme123" \
  docker.elastic.co/elasticsearch/elasticsearch:7.17.0

echo -e "${BLUE}Waiting for Elasticsearch to be ready...${NC}"
for i in {1..30}; do
    if curl -s -u elastic:changeme123 -o /dev/null -w "%{http_code}" "http://localhost:9200/_cluster/health" | grep -q "200"; then
        echo -e "${GREEN}✓ Elasticsearch is ready${NC}"
        break
    fi
    echo -n "."
    sleep 2
done

if ! curl -s -u elastic:changeme123 -o /dev/null -w "%{http_code}" "http://localhost:9200/_cluster/health" | grep -q "200"; then
    echo -e "\n${RED}Error: Elasticsearch failed to start properly${NC}"
    exit 1
fi

# Step 3: Create index and add documents
echo -e "\n${YELLOW}Step 3: Setting up Elasticsearch index and documents...${NC}"

# Create the index with mapping
echo -e "${BLUE}Creating partnership-documents index...${NC}"
curl -s -u elastic:changeme123 -X PUT "localhost:9200/partnership-documents" \
  -H "Content-Type: application/json" \
  -d @"$SCRIPT_DIR/setup-elasticsearch.json" > /dev/null

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Index created successfully${NC}"
else
    echo -e "${RED}Error: Failed to create index${NC}"
    exit 1
fi

# Index sample documents
echo -e "${BLUE}Indexing sample documents...${NC}"
curl -s -u elastic:changeme123 -X POST "localhost:9200/partnership-documents/_bulk" \
  -H "Content-Type: application/json" \
  --data-binary @"$SCRIPT_DIR/sample-documents-bulk.json" > /dev/null

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Sample documents indexed successfully${NC}"
else
    echo -e "${RED}Error: Failed to index sample documents${NC}"
    exit 1
fi

# Step 4: Set user secrets for the Web API
echo -e "\n${YELLOW}Step 4: Configuring user secrets for Web API...${NC}"
cd "$PROJECT_ROOT/src/PartnershipAgent.WebApi"

dotnet user-secrets set "ElasticSearch:Username" "elastic"
dotnet user-secrets set "ElasticSearch:Password" "changeme123"
dotnet user-secrets set "ElasticSearch:Uri" "http://localhost:9200"

echo -e "${GREEN}✓ User secrets configured${NC}"

# Step 5: Build the solution
echo -e "\n${YELLOW}Step 5: Building the solution...${NC}"
cd "$PROJECT_ROOT"

dotnet build PartnershipAgent.sln

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Solution built successfully${NC}"
else
    echo -e "${RED}Error: Failed to build solution${NC}"
    exit 1
fi

# Step 6: Start the Web API in background
echo -e "\n${YELLOW}Step 6: Starting Web API...${NC}"
cd "$PROJECT_ROOT/src/PartnershipAgent.WebApi"

# Kill any existing dotnet processes on port 5001
pkill -f "dotnet.*PartnershipAgent.WebApi" || true
lsof -ti:5001 | xargs kill -9 2>/dev/null || true

# Start the Web API in background
dotnet run --urls="http://localhost:5001" > /tmp/webapi.log 2>&1 &
WEBAPI_PID=$!

echo -e "${BLUE}Web API started with PID: $WEBAPI_PID${NC}"
echo -e "${BLUE}Waiting for Web API to be ready...${NC}"

# Wait for Web API to be ready
for i in {1..30}; do
    if curl -s -o /dev/null -w "%{http_code}" "http://localhost:5001/api/chat/health" | grep -q "200"; then
        echo -e "${GREEN}✓ Web API is ready${NC}"
        break
    fi
    echo -n "."
    sleep 2
done

if ! curl -s -o /dev/null -w "%{http_code}" "http://localhost:5001/api/chat/health" | grep -q "200"; then
    echo -e "\n${RED}Error: Web API failed to start properly${NC}"
    echo -e "${YELLOW}Web API logs:${NC}"
    tail -20 /tmp/webapi.log
    kill $WEBAPI_PID 2>/dev/null || true
    exit 1
fi

# Step 7: Run the console app with test prompt
echo -e "\n${YELLOW}Step 7: Running console app with test prompt...${NC}"
cd "$PROJECT_ROOT/src/PartnershipAgent.ConsoleApp"

echo -e "${BLUE}Sending test prompt: 'What are partnership terms and requirements?'${NC}"
echo -e "${GREEN}==================== CONSOLE APP OUTPUT ====================${NC}"

# Run the console app and send test prompt
(
    sleep 1
    echo "What are partnership terms and requirements?"
    sleep 30
    echo "quit"
) | timeout 60s dotnet run || true

echo -e "${GREEN}==================== END CONSOLE APP OUTPUT ====================${NC}"

# Cleanup
echo -e "\n${YELLOW}Cleaning up...${NC}"
kill $WEBAPI_PID 2>/dev/null || true
echo -e "${GREEN}✓ Web API stopped${NC}"

echo -e "\n${GREEN}🎉 Setup completed successfully!${NC}"
echo -e "\n${BLUE}Summary:${NC}"
echo -e "• Elasticsearch is running at: ${YELLOW}http://localhost:9200${NC}"
echo -e "• Username: ${YELLOW}elastic${NC}"
echo -e "• Password: ${YELLOW}changeme123${NC}"
echo -e "• Index: ${YELLOW}partnership-documents${NC}"
echo -e "• Sample documents have been indexed"
echo -e "\n${BLUE}To manually start the Web API:${NC}"
echo -e "cd src/PartnershipAgent.WebApi && dotnet run --urls=\"http://localhost:5001\""
echo -e "\n${BLUE}To manually run the console app:${NC}"
echo -e "cd src/PartnershipAgent.ConsoleApp && dotnet run"
echo -e "\n${BLUE}To stop Elasticsearch:${NC}"
echo -e "docker stop elasticsearch-secure"