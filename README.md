# Partnership Agent

An AI-powered partnership management system featuring **advanced citation functionality** and **high-performance vector search** that provides precise document references when answering questions about partnership agreements, revenue sharing, compliance, and operational requirements.

Built with Azure OpenAI, Azure AI Search (or Elasticsearch), ASP.NET Core, and enhanced with intelligent document citation tracking and sub-second document retrieval.

## 🚀 Quick Start (Recommended)

### ⭐ **Preferred Method: Cross-Platform .NET Setup**

Works identically on **Windows, macOS, and Linux**:

```bash
# Linux/macOS - Default (InMemory chat history, Elasticsearch)
cd setup
./setup.sh

# Linux/macOS - High-performance vector search with Azure AI Search
cd setup
./setup.sh inmemory --vector-search

# Linux/macOS - SQLite chat history with vector search
cd setup
./setup.sh sqlite --vector-search

# Linux/macOS - Azure SQL chat history  
cd setup
./setup.sh azuresql

# Windows Command Prompt
cd setup
setup.cmd [inmemory|sqlite|azuresql] [--vector-search]

# Windows PowerShell  
cd setup
.\setup.cmd [inmemory|sqlite|azuresql] [--vector-search]
```

**Why this is preferred:**
- ✅ Single codebase - no platform differences
- ✅ Better error handling and logging
- ✅ Consistent behavior across all operating systems
- ✅ Professional development practices
- ✅ Easier to maintain and extend
- 🚀 **High-performance vector search option** for production workloads

## 🔧 Alternative Setup Methods

### Platform-Specific Scripts (Legacy)

**Linux/macOS (Bash):**
```bash
./setup/start-partnership-agent.sh
```

**Windows (PowerShell):**
```powershell
.\setup\start-partnership-agent.ps1
```

### Manual .NET Execution
```bash
cd setup
dotnet run --project setup.csproj [inmemory|sqlite|azuresql] [--vector-search]

# Examples:
dotnet run                           # Uses InMemory (default) + Elasticsearch
dotnet run inmemory --vector-search  # Uses InMemory + Azure AI Search (high performance)
dotnet run sqlite                    # Uses SQLite + Elasticsearch
dotnet run sqlite --vector-search   # Uses SQLite + Azure AI Search (recommended)
dotnet run azuresql                  # Uses Azure SQL Database
```

## 🔍 Search Engine Options

The Partnership Agent supports two search engine configurations:

### 🚀 **Azure AI Search (Vector Search) - Recommended for Production**
- **Best for**: Production deployments, high-performance requirements
- **Performance**: Sub-second document retrieval (99%+ faster than Elasticsearch)
- **Technology**: Vector embeddings with semantic similarity search  
- **Setup**: Requires Azure AI Search service configuration
- **Cost**: ~$250/month for Basic tier
- **Benefits**: Semantic understanding, automatic scaling, managed service

### 🗃️ **Elasticsearch (Traditional Text Search) - Default**
- **Best for**: Development, testing, offline scenarios
- **Performance**: 60-120 seconds for document search
- **Technology**: Text-based search with keyword matching
- **Setup**: Automatic Docker container deployment
- **Cost**: Free (local Docker container)
- **Benefits**: No cloud dependencies, familiar technology

## 💾 Chat History Options

The Partnership Agent supports three chat history storage options:

### 🧠 **InMemory (Default)**
- **Best for**: Development, testing, quick demos
- **Persistence**: No persistence - data lost on restart
- **Setup**: Zero configuration required
- **Performance**: Fastest startup and response times

### 🗄️ **SQLite (Recommended for Local Development)**
- **Best for**: Local development with persistence, offline work
- **Persistence**: Full persistence in local SQLite database
- **Setup**: Automatic Docker container with volume mounting
- **Performance**: Fast local file-based storage
- **Container**: `partnership-agent-sqlite` with persistent volume

### ☁️ **Azure SQL Database**
- **Best for**: Production deployments, multi-user scenarios
- **Persistence**: Full persistence in cloud database
- **Setup**: Requires Azure SQL Database and connection configuration
- **Performance**: Network-dependent, highly scalable
- **Features**: Enterprise-grade reliability and backup

## 🎯 What Gets Set Up

The setup process configures a complete partnership management system with **citation-enhanced AI responses**:

### 🗃️ **Enhanced Document Index**
- **8 comprehensive documents** with realistic partnership content
- **Rich metadata** including source paths, modification dates, and versioning
- **Citation-optimized content** with specific percentages, requirements, and procedures

### 📋 **Document Categories:**
1. **Partnership Agreement Template** - Formation, revenue tiers, contribution requirements
2. **Revenue Sharing Guidelines** - Detailed calculation methods, payment terms
3. **Partnership Compliance Requirements** - Documentation standards, audit procedures
4. **Standard Partnership Contract** - IP rights, liability, termination procedures
5. **Partner Onboarding Process** - Assessment criteria, training requirements
6. **Dispute Resolution Framework** - Escalation procedures, mediation processes
7. **Performance Metrics and KPIs** - Revenue metrics, operational standards
8. **Data Security and Privacy Policy** - Classification levels, security controls

### 🔍 **Citation Features**
- **Precise text excerpts** with character-level positioning
- **Relevance scoring** based on query-answer term matching
- **Context awareness** with before/after text snippets
- **Multi-source references** from multiple documents
- **Document metadata** including categories and modification dates

## 🧪 Testing Citation Functionality

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
   - Expected citations from Partnership Agreement Template and Revenue Sharing Guidelines
   - Should show specific percentages: Tier 1 (30-35%), Tier 2 (20-25%), Tier 3 (10-15%)

2. **"What is the minimum credit score requirement for partner verification?"**
   - Expected citation from Partnership Compliance Requirements
   - Should reference the 650 minimum credit score

3. **"How long is the notice period for partnership termination?"**
   - Expected citation from Standard Partnership Contract
   - Should reference the 90-day written notice requirement

## 🏗️ Architecture Overview

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Console App   │    │     Web API      │    │   Elasticsearch │
│                 │◄──►│                  │◄──►│                 │
│ - Interactive   │    │ - REST endpoints │    │ - Document      │
│   chat          │    │ - Step           │    │   indexing      │
│ - Test prompts  │    │   orchestration  │    │ - Search        │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │   Citation       │
                       │   Engine         │
                       │                  │
                       │ - Text analysis  │
                       │ - Excerpt        │
                       │   extraction     │
                       │ - Relevance      │
                       │   scoring        │
                       └──────────────────┘
                                │
                                ▼
                    ┌──────────────────────────┐
                    │    Chat History          │
                    │    Storage               │
                    ├──────────────────────────┤
                    │ 🧠 InMemory (Default)    │
                    │ 🗄️ SQLite Container      │
                    │ ☁️ Azure SQL Database    │
                    └──────────────────────────┘
```

## 🔌 Running the Applications

After setup, you can run the applications individually:

### Web API
```bash
cd src/PartnershipAgent.WebApi
dotnet run --urls="http://localhost:5001"
```
- REST API available at `http://localhost:5001`
- Swagger UI at `http://localhost:5001/swagger`
- Health check at `http://localhost:5001/api/chat/health`

### Console Application
```bash
cd src/PartnershipAgent.ConsoleApp
dotnet run
```
- Interactive chat interface
- Type questions about partnerships
- See detailed citations in responses
- Type 'quit' to exit

### Example API Request
```bash
curl -X POST "http://localhost:5001/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "threadId": "test-123",
    "prompt": "What are the revenue sharing percentages?"
  }'
```

## 📊 Citation Response Format

Responses now include detailed citations:

```json
{
  "answer": "Revenue sharing is structured in tiers...",
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
  "follow_up_suggestions": [...]
}
```

## 🛠️ Requirements

### Essential Requirements
- **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **Docker** - [Docker Desktop](https://www.docker.com/products/docker-desktop) (Windows) or Docker Engine (Linux/macOS)
  - Required for Elasticsearch (default search)
  - Required for SQLite chat history (optional)
- **Azure OpenAI** - Access to Azure OpenAI service with deployed model
- **Internet connection** - For downloading Docker images

### Optional Requirements (Based on Configuration)
- **Azure AI Search** - Required for high-performance vector search (`--vector-search` option)
- **Azure SQL Database** - Only required if using Azure SQL chat history option

### For High-Performance Vector Search
When using `--vector-search` option, you'll need:
- **Azure AI Search service** (Basic tier recommended)
- **Azure OpenAI with embeddings model** (text-embedding-ada-002)
- **Additional Azure costs** (~$250/month for Basic tier)

## 📚 Documentation

Detailed guides available in the `/docs` directory:

- **[Setup Instructions](docs/setup-instructions.md)** - Complete manual setup guide
- **[Azure OpenAI Setup](docs/azure-openai-setup.md)** - Azure OpenAI configuration
- **[Azure AI Search Setup](docs/azure-ai-search-setup.md)** - 🚀 High-performance vector search configuration
- **[Azure Key Vault Setup](docs/azure-keyvault-setup.md)** - Enterprise credential management
- **[Credential Management](docs/credential-management.md)** - Security best practices
- **[Monitoring and Telemetry](docs/monitoring-and-telemetry.md)** - 📊 Application Insights queries for evaluation metrics and performance analysis
- **[Citation Testing Guide](setup/TESTING_CITATIONS.md)** - Comprehensive citation testing

## 🧩 Project Structure

```
partnership-agent/
├── src/
│   ├── PartnershipAgent.Core/          # Core business logic & citation engine
│   ├── PartnershipAgent.WebApi/        # REST API endpoints
│   ├── PartnershipAgent.ConsoleApp/    # Interactive console interface
│   └── PartnershipAgent.Core.Tests/    # Unit tests including citation tests
├── setup/                              # Setup scripts and tools
│   ├── setup.sh / setup.cmd           # 🌟 Cross-platform setup (PREFERRED)
│   ├── start-partnership-agent.sh     # Legacy bash script
│   ├── start-partnership-agent.ps1    # Legacy PowerShell script
│   ├── test-citations.sh/ps1          # Citation testing scripts
│   ├── CrossPlatformSetup.cs          # .NET setup application
│   └── sample-documents-bulk.json     # Enhanced sample documents
└── docs/                               # Documentation
```

## 🔍 Key Features

### Citation Engine
- **Smart text analysis** with relevance scoring
- **Precise positioning** with character-level accuracy
- **Context extraction** for better understanding
- **Multi-document synthesis** from various sources

### Document Processing
- **Enhanced metadata** including source paths and timestamps
- **Categorized content** (templates, guidelines, policies, contracts)
- **Structured indexing** optimized for citation extraction

### AI Integration
- **Azure OpenAI** for natural language understanding
- **Semantic Kernel** for agent orchestration
- **Step-based processing** with entity resolution and response generation

## ⚙️ Configuration Options

### Chat History Provider Configuration

You can configure chat history storage through user secrets or directly:

```bash
# Set chat history provider
dotnet user-secrets set "ChatHistory:Provider" "sqlite"

# Configure SQLite connection (if using SQLite)
dotnet user-secrets set "SQLite:ConnectionString" "Data Source=/data/partnership-agent.db;Cache=Shared"

# Configure Azure SQL connection (if using Azure SQL)
dotnet user-secrets set "AzureSQL:ConnectionString" "your-azure-sql-connection-string"
```

### Runtime Chat History Switching

You can also switch providers at runtime by modifying `appsettings.json`:

```json
{
  "ChatHistory": {
    "Provider": "sqlite"  // "inmemory", "sqlite", or "azuresql"
  },
  "SQLite": {
    "ConnectionString": "Data Source=/data/partnership-agent.db;Cache=Shared"
  }
}
```

## 🚦 Getting Started Steps

1. **Clone the repository**
2. **Choose your configuration**:
   - `./setup/setup.sh` (InMemory + Elasticsearch - fastest setup)
   - `./setup/setup.sh sqlite` (SQLite + Elasticsearch - persistent, local)
   - `./setup/setup.sh sqlite --vector-search` (SQLite + Azure AI Search - high performance)
   - `./setup/setup.sh azuresql` (Azure SQL - production-ready)
3. **Configure Azure AI Search** (if using `--vector-search`):
   - See [Azure AI Search Setup Guide](docs/azure-ai-search-setup.md)
4. **Test with sample queries** about revenue sharing, compliance, or termination procedures
5. **Explore the citation responses** to see precise document references
6. **Try the console app** for interactive testing with persistent chat history

## 🤝 Contributing

When contributing to this project:
- Use the **cross-platform .NET setup** for consistency
- Run the citation tests to ensure functionality
- Follow the existing code patterns for citation extraction
- Update documentation for any new citation features

## 📄 License

This project is provided as sample code for educational and demonstration purposes.