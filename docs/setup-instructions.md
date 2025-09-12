# Partnership Agent Setup Instructions

## Prerequisites
- .NET 8.0 SDK
- Azure OpenAI Service resource with a deployed model (e.g., GPT-3.5-turbo or GPT-4)
- ElasticSearch instance (local or cloud)

## Configuration

### 1. Set your Azure OpenAI Configuration

⚠️ **NEVER store credentials in source control!**

**Option A: User Secrets (Development - RECOMMENDED)**
```bash
cd src/PartnershipAgent.WebApi
dotnet user-secrets init  # Already done for this project

# Set your actual Azure OpenAI credentials
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource-name.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-azure-openai-api-key-here"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-35-turbo"
```

**Option B: Environment Variables (Production)**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-azure-openai-api-key-here"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-35-turbo"
```

**Option C: Azure Key Vault (Enterprise)**
See `azure-keyvault-setup.md` for enterprise-grade credential management.

📖 **For detailed credential management practices, see `credential-management.md`**

### 2. Set up ElasticSearch

**Update `appsettings.json` with your ElasticSearch details:**
```json
{
  "ElasticSearch": {
    "Uri": "https://your-elasticsearch-cluster.com:9243",
    "Username": "your-username",
    "Password": "your-password"
  }
}
```

**Create the ElasticSearch index:**
```bash
# Create index with mapping
curl -X PUT "your-elasticsearch-cluster.com:9243/partnership-documents" \
  -H "Content-Type: application/json" \
  -u username:password \
  -d @setup-elasticsearch.json

# Index sample documents
curl -X POST "your-elasticsearch-cluster.com:9243/partnership-documents/_bulk" \
  -H "Content-Type: application/json" \
  -u username:password \
  --data-binary @sample-documents-bulk.json
```

**For local ElasticSearch (Docker):**
```bash
# Run ElasticSearch locally
docker run -d --name elasticsearch \
  -p 9200:9200 -p 9300:9300 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  docker.elastic.co/elasticsearch/elasticsearch:7.17.0

# Create index and sample data
curl -X PUT "localhost:9200/partnership-documents" \
  -H "Content-Type: application/json" \
  -d @setup-elasticsearch.json
```

## Running the Application

### 1. Start the Web API
```bash
cd src/PartnershipAgent.WebApi
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:7000
- Swagger UI: https://localhost:7000/swagger

### 2. Run the Console Application
In a new terminal:
```bash
cd src/PartnershipAgent.ConsoleApp
dotnet run
```

## API Endpoints

### POST /api/chat
Send a chat message with the following JSON structure:
```json
{
  "threadId": "your-conversation-id",
  "prompt": "What are the revenue sharing terms for Tier 1 partners?"
}
```

The API will automatically set mock values for `userId` and `tenantId`.

Response includes:
- Generated response from the FAQ agent (using Azure OpenAI)
- Extracted entities from the prompt (using Azure OpenAI)
- Relevant documents found in ElasticSearch

### Example Response:
```json
{
  "threadId": "your-conversation-id",
  "response": "Based on the partnership documents, Tier 1 partners receive 30% of net revenue according to our Revenue Sharing Guidelines...",
  "extractedEntities": ["Tier 1 partners", "revenue sharing", "partnership terms"],
  "relevantDocuments": [
    {
      "id": "doc2",
      "title": "Revenue Sharing Guidelines", 
      "content": "Revenue sharing should be based on contribution levels: Tier 1 partners receive 30%...",
      "category": "guidelines",
      "score": 0.95,
      "tenantId": "mock-tenant-456"
    }
  ]
}
```

### GET /api/chat/health
Health check endpoint

## Architecture Overview

The application uses a Semantic Kernel Process Framework with two main agents:

1. **EntityResolutionAgent**: Extracts entities like company names, dates, financial amounts, etc.
2. **FAQAgent**: Searches ElasticSearch documents and generates responses based on partnership agreement data

The process flow:
1. User sends prompt → EntityResolutionAgent extracts entities
2. FAQAgent searches for relevant documents based on tenant permissions
3. FAQAgent generates response using retrieved documents
4. Final response includes all extracted information

## Multi-Tenant Support

- Documents are filtered by `tenantId`
- Users can only access documents in allowed categories
- Mock tenant/user IDs are currently used (replace with JWT middleware)

## Testing

You can test the API using:
- The included console application
- Swagger UI at https://localhost:7000/swagger
- Any HTTP client (Postman, curl, etc.)

Example curl command:
```bash
curl -X POST https://localhost:7000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"threadId":"test-123","prompt":"What are the partnership terms?"}'
```