# Partnership Agent Setup Instructions

## 🚀 Quick Start (Recommended)

### ⭐ **Preferred: Automated Cross-Platform Setup**

The easiest way to get started is with our cross-platform setup tool:

```bash
# Linux/macOS
cd setup
./setup.sh

# Windows Command Prompt
cd setup
setup.cmd

# Windows PowerShell
cd setup
.\setup.cmd
```

**This automated setup will:**
- ✅ Start Elasticsearch in Docker
- ✅ Create the document index with enhanced mapping
- ✅ Load 8 sample documents with citation-rich content
- ✅ Configure user secrets
- ✅ Build the solution
- ✅ Test the citation functionality

**Skip to the [Testing Citations](#testing-citations) section if using automated setup.**

## 📋 Manual Setup (Advanced Users)

For custom configurations or learning purposes, follow the manual setup below.

### Prerequisites
- .NET 8.0 SDK
- Azure OpenAI Service resource with a deployed model (e.g., GPT-3.5-turbo or GPT-4)
- Docker (for Elasticsearch)

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

### 2. Set up Elasticsearch

**Local Elasticsearch (Docker - Recommended for Development):**
```bash
# Run Elasticsearch locally
docker run -d --name elasticsearch \
  -p 9200:9200 -p 9300:9300 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  -e "ES_JAVA_OPTS=-Xms512m -Xmx512m" \
  docker.elastic.co/elasticsearch/elasticsearch:7.17.0

# Wait for Elasticsearch to start, then create index
curl -X PUT "localhost:9200/partnership-documents" \
  -H "Content-Type: application/json" \
  -d @setup/setup-elasticsearch.json

# Load enhanced sample documents with citation content
curl -X POST "localhost:9200/partnership-documents/_bulk" \
  -H "Content-Type: application/json" \
  --data-binary @setup/sample-documents-bulk.json
```

**Cloud Elasticsearch:**
Update `appsettings.json` or user secrets with your cloud cluster details:
```bash
dotnet user-secrets set "ElasticSearch:Uri" "https://your-elasticsearch-cluster.com:9243"
dotnet user-secrets set "ElasticSearch:Username" "your-username"
dotnet user-secrets set "ElasticSearch:Password" "your-password"
```

### 3. Build the Solution
```bash
dotnet build PartnershipAgent.sln
```

## 🔌 Running the Applications

### 1. Start the Web API
```bash
cd src/PartnershipAgent.WebApi
dotnet run --urls="http://localhost:5001"
```

The API will be available at:
- HTTP: http://localhost:5001
- Swagger UI: http://localhost:5001/swagger
- Health check: http://localhost:5001/api/chat/health

### 2. Run the Console Application
In a new terminal:
```bash
cd src/PartnershipAgent.ConsoleApp
dotnet run
```

## 🧪 Testing Citations

### Quick Citation Tests
```bash
# Linux/macOS
./setup/test-citations.sh

# Windows
.\setup\test-citations.ps1
```

### Example Citation Queries
Try these prompts to see rich citations in action:

1. **"What are the revenue sharing percentages for different partner tiers?"**
2. **"What is the minimum credit score requirement for partner verification?"**
3. **"How long is the notice period for partnership termination?"**
4. **"What customer satisfaction scores are required for partners?"**

## 📊 API Endpoints

### POST /api/chat
Send a chat message with the following JSON structure:
```json
{
  "threadId": "your-conversation-id",
  "prompt": "What are the revenue sharing terms for Tier 1 partners?"
}
```

The API will automatically set mock values for `userId` and `tenantId`.

### Enhanced Response Format with Citations
```json
{
  "threadId": "your-conversation-id",
  "response": "Based on the partnership documents, Tier 1 partners receive 30% of net revenue...",
  "extractedEntities": ["Tier 1 partners", "revenue sharing"],
  "relevantDocuments": [
    {
      "id": "doc2",
      "title": "Revenue Sharing Guidelines",
      "content": "Revenue sharing should be based on contribution levels...",
      "category": "guidelines",
      "score": 0.95,
      "tenantId": "tenant-123"
    }
  ],
  "structuredResponse": {
    "answer": "Revenue sharing is structured in multiple tiers...",
    "confidence_level": "high",
    "source_documents": ["Revenue Sharing Guidelines", "Partnership Agreement Template"],
    "citations": [
      {
        "document_id": "doc2",
        "document_title": "Revenue Sharing Guidelines",
        "category": "guidelines",
        "excerpt": "Tier 1 (Strategic Partners): 30% of net revenue, paid monthly",
        "start_position": 1450,
        "end_position": 1502,
        "relevance_score": 0.92,
        "context_before": "Partner shares are distributed according to contribution tiers:",
        "context_after": "- Tier 2 (Operational Partners): 20% of net revenue"
      }
    ],
    "has_complete_answer": true,
    "follow_up_suggestions": [
      "What are the payment terms for revenue sharing?",
      "How are partner performance metrics calculated?"
    ]
  }
}
```

### GET /api/chat/health
Health check endpoint

## 🏗️ Architecture Overview

The application uses a Semantic Kernel Process Framework with enhanced citation capabilities:

### Core Components:
1. **EntityResolutionAgent**: Extracts entities like company names, dates, financial amounts
2. **FAQAgent**: Searches documents and generates responses with citations
3. **CitationService**: Extracts precise text excerpts and relevance scores
4. **ElasticsearchService**: Enhanced document indexing and search

### Citation Processing Flow:
1. User sends prompt → EntityResolutionAgent extracts entities
2. FAQAgent searches for relevant documents based on tenant permissions
3. CitationService analyzes documents and extracts relevant excerpts
4. FAQAgent generates response with detailed citations
5. Response includes structured citations with precise positioning

### Enhanced Document Structure:
- **Rich metadata**: Source paths, modification dates, versioning
- **Categorized content**: Templates, guidelines, policies, contracts
- **Citation-optimized**: Structured sections with specific data points

## 🎯 Sample Documents

The setup includes 8 comprehensive documents:

1. **Partnership Agreement Template** - Revenue tiers, contribution requirements
2. **Revenue Sharing Guidelines** - Calculation methods, payment terms
3. **Partnership Compliance Requirements** - Documentation, audit procedures
4. **Standard Partnership Contract** - IP rights, liability, termination
5. **Partner Onboarding Process** - Assessment criteria, training
6. **Dispute Resolution Framework** - Escalation, mediation procedures
7. **Performance Metrics and KPIs** - Revenue metrics, operational standards
8. **Data Security and Privacy Policy** - Classification, security controls

## 🔒 Multi-Tenant Support

- Documents are filtered by `tenantId`
- Users can only access documents in allowed categories
- Mock tenant/user IDs are currently used (replace with JWT middleware in production)

## 🧪 Testing Options

You can test the system using:

1. **Automated citation tests**: `./setup/test-citations.sh`
2. **Console application**: Interactive chat with citation display
3. **Swagger UI**: http://localhost:5001/swagger
4. **Direct API calls**: Using curl, Postman, etc.

### Example curl command:
```bash
curl -X POST http://localhost:5001/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "threadId": "test-123",
    "prompt": "What are the revenue sharing percentages for different partner tiers?"
  }'
```

## 🔧 Available Setup Methods

| Method | Platform | Use Case |
|--------|----------|----------|
| **`./setup/setup.sh`** (Recommended) | Cross-platform | ✅ Preferred for all users |
| `./setup/start-partnership-agent.sh` | Linux/macOS | Legacy bash script |
| `.\setup\start-partnership-agent.ps1` | Windows | Legacy PowerShell script |
| `dotnet run --project setup/setup.csproj` | Cross-platform | Direct .NET execution |

The cross-platform .NET setup (`setup.sh`/`setup.cmd`) is recommended for:
- Consistent behavior across all operating systems
- Better error handling and logging
- Professional development practices
- Easier maintenance and extension

## 🚀 Next Steps

After setup:
1. **Test citation queries** using the sample prompts
2. **Explore the console application** for interactive testing
3. **Review citation responses** to understand the format
4. **Try custom queries** about partnerships, compliance, or revenue sharing
5. **Examine the code** to understand citation extraction algorithms