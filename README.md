# Partnership Agent

An AI-powered partnership management system featuring **advanced citation functionality** that provides precise document references when answering questions about partnership agreements, revenue sharing, compliance, and operational requirements.

Built with Azure OpenAI, Elasticsearch, ASP.NET Core, and enhanced with intelligent document citation tracking.

## 🚀 Quick Start (Recommended)

### ⭐ **Preferred Method: Cross-Platform .NET Setup**

Works identically on **Windows, macOS, and Linux**:

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

**Why this is preferred:**
- ✅ Single codebase - no platform differences
- ✅ Better error handling and logging
- ✅ Consistent behavior across all operating systems
- ✅ Professional development practices
- ✅ Easier to maintain and extend

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
dotnet run --project setup.csproj
```

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

- **.NET 8 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **Docker** - [Docker Desktop](https://www.docker.com/products/docker-desktop) (Windows) or Docker Engine (Linux/macOS)
- **Azure OpenAI** - Access to Azure OpenAI service with deployed model
- **Internet connection** - For downloading Elasticsearch Docker image

## 📚 Documentation

Detailed guides available in the `/docs` directory:

- **[Setup Instructions](docs/setup-instructions.md)** - Complete manual setup guide
- **[Azure OpenAI Setup](docs/azure-openai-setup.md)** - Azure OpenAI configuration
- **[Azure Key Vault Setup](docs/azure-keyvault-setup.md)** - Enterprise credential management
- **[Credential Management](docs/credential-management.md)** - Security best practices
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

## 🚦 Getting Started Steps

1. **Clone the repository**
2. **Run the preferred setup**: `./setup/setup.sh` (or `setup.cmd` on Windows)
3. **Test with sample queries** about revenue sharing, compliance, or termination procedures
4. **Explore the citation responses** to see precise document references
5. **Try the console app** for interactive testing

## 🤝 Contributing

When contributing to this project:
- Use the **cross-platform .NET setup** for consistency
- Run the citation tests to ensure functionality
- Follow the existing code patterns for citation extraction
- Update documentation for any new citation features

## 📄 License

This project is provided as sample code for educational and demonstration purposes.