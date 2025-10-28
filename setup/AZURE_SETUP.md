# Azure Live Test Setup Guide

This guide helps you test the Partnership Agent with **Agent Framework v2** using live Azure resources.

## Prerequisites

Before running the test script, ensure you have:

### 1. Azure OpenAI Resource
- **Endpoint**: Your Azure OpenAI resource URL (e.g., `https://your-resource.openai.azure.com`)
- **API Key**: From Azure Portal → Your OpenAI Resource → Keys and Endpoint
- **Deployments**:
  - Chat model (e.g., `gpt-4o`, `gpt-4`, or `gpt-35-turbo`)
  - Embedding model (e.g., `text-embedding-ada-002`)

### 2. Azure AI Search Resource
- **Service Name**: Your search service name (e.g., `my-search-service`)
  - Full endpoint will be: `https://my-search-service.search.windows.net`
- **API Key**: Admin key from Azure Portal → Your Search Service → Keys
- **Index**: Will be created automatically by the script (`partnership-documents-vector`)

### 3. Azure SQL Database
- **Connection String**: From Azure Portal → Your SQL Database → Connection strings
- **Table Setup**: Run the SQL script first:

```bash
# Connect to your Azure SQL database and run:
setup/azure-sql-setup.sql
```

This creates the `ChatMessages` table for storing conversation history.

## Quick Start

### Option 1: Interactive Setup (Recommended for First Time)

```bash
./setup/test-azure-live.sh
```

This script will:
1. Prompt for your Azure resource credentials
2. Save configuration to `.env.azure-test` for reuse
3. Build and start the API
4. Initialize Azure AI Search index
5. Index sample partnership documents
6. Run test queries to verify everything works
7. Show Agent Framework v2 agents in action

### Option 2: Reuse Saved Configuration

If you've run the script before:

```bash
# Load saved configuration
source setup/.env.azure-test

# Start the API
dotnet run --project src/PartnershipAgent.WebApi
```

### Option 3: Manual Configuration

Set environment variables manually:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_API_KEY="your-key"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME="text-embedding-ada-002"

export AzureSearch__Endpoint="https://your-service.search.windows.net"
export AzureSearch__ApiKey="your-search-key"
export AzureSearch__UseVectorSearch="true"

export ChatHistory__Provider="AzureSQL"
export AZURESQL_CONNECTION_STRING="Server=tcp:your-server.database.windows.net,1433;Initial Catalog=your-db;User ID=your-user;Password=your-password;..."

# Start the API
dotnet run --project src/PartnershipAgent.WebApi
```

## Testing the Agent Framework v2

Once the API is running, you can test the Agent Framework agents:

### Test Streaming Endpoint

```bash
curl -X POST http://localhost:5000/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"threadId":"test-123","prompt":"What are the partnership revenue sharing tiers?"}'
```

You should see:
- ✅ `ScopingAgentV2` validating the query is in-scope
- ✅ `EntityResolutionAgentV2` extracting entities
- ✅ `DocumentSearchAgentV2` finding relevant documents in Azure AI Search
- ✅ `ResponseGenerationAgentV2` streaming the answer with citations

### Test Standard Endpoint

```bash
curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"threadId":"test-456","prompt":"Tell me about partnership compliance requirements"}'
```

### Check Chat History in Azure SQL

```sql
-- Connect to your Azure SQL database
SELECT
    ThreadId,
    Role,
    SUBSTRING(Content, 1, 100) AS ContentPreview,
    DateInserted
FROM dbo.ChatMessages
ORDER BY DateInserted DESC;
```

## Admin Endpoints

The API includes admin endpoints for managing documents:

### Initialize Vector Search

```bash
curl -X POST http://localhost:5000/api/admin/initialize-vector-search
```

### Reindex Sample Documents

```bash
curl -X POST http://localhost:5000/api/admin/reindex-documents
```

### Check Configuration

```bash
curl http://localhost:5000/api/admin/check-config
```

## Sample Partnership Documents

The system includes 5 sample documents about:
1. Partnership Agreement Templates
2. Revenue Sharing Guidelines (Tier 1: 30-35%, Tier 2: 20-25%, Tier 3: 10-15%)
3. Partnership Compliance Requirements
4. Standard Partnership Contracts
5. Performance Metrics and KPIs

These documents are automatically indexed when you run the setup script.

## Troubleshooting

### API won't start

```bash
# Check if ports are in use
lsof -i :5000

# View detailed logs
dotnet run --project src/PartnershipAgent.WebApi --verbosity detailed
```

### Azure SQL connection fails

- Verify your IP is allowed in Azure SQL firewall rules
- Test connection string with `sqlcmd` or Azure Data Studio
- Ensure the `ChatMessages` table exists (run `azure-sql-setup.sql`)

### Azure AI Search indexing fails

- Verify your API key has admin permissions (not just query)
- Check that embedding deployment exists in Azure OpenAI
- View logs in Application Insights if configured

### No documents found in search

```bash
# Verify documents are indexed
curl -X POST http://localhost:5000/api/admin/reindex-documents

# Check Azure AI Search portal to see document count
```

## Architecture

This setup demonstrates the complete Agent Framework v2 migration:

- **V2 Agents**:
  - `ScopingAgentV2` - ChatClientAgent with structured validation
  - `EntityResolutionAgentV2` - Entity extraction
  - `DocumentSearchAgentV2` - Vector search via Azure AI Search
  - `ResponseGenerationAgentV2` - Streaming answer generation

- **Infrastructure**:
  - Azure OpenAI (OpenAI SDK 2.5.0 via Microsoft.Extensions.AI)
  - Azure AI Search (Vector embeddings for semantic search)
  - Azure SQL (Persistent chat history)
  - Application Insights (Telemetry and observability)

## Next Steps

After successful testing:

1. **Monitor Performance**: Check Application Insights for traces
2. **Review Chat History**: Query Azure SQL to see conversation persistence
3. **Scale Documents**: Add your own partnership documents via admin endpoints
4. **Production Setup**: Configure managed identities instead of API keys
5. **Cleanup V1**: Remove Semantic Kernel v1 agents from Core project

## Cost Considerations

Running this test incurs Azure costs:
- Azure OpenAI: Pay per token (chat + embeddings)
- Azure AI Search: Hourly charge based on tier
- Azure SQL: DTU/vCore pricing
- Application Insights: Log ingestion charges

Estimate: ~$0.50-$2 for a test session with sample queries.
