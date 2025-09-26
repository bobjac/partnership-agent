# Partnership Agent Setup Scripts

This directory contains multiple setup options for the Partnership Agent with enhanced citation functionality and **multi-provider chat history support**.

## 🚀 Quick Start (Recommended)

### ⭐ **Cross-Platform Setup (PREFERRED)**
**Works on Windows, macOS, and Linux**

```bash
# Linux/macOS - Default (InMemory chat history)
./setup.sh

# Linux/macOS - With persistent SQLite chat history
./setup.sh sqlite

# Linux/macOS - With Azure SQL chat history
./setup.sh azuresql

# Windows Command Prompt
setup.cmd [inmemory|sqlite|azuresql]

# Windows PowerShell
.\setup.cmd [inmemory|sqlite|azuresql]
```

**Why this is preferred:**
- ✅ Single codebase - consistent across all platforms
- ✅ Better error handling and professional logging
- ✅ **Multi-provider chat history support** (InMemory, SQLite, Azure SQL)
- ✅ **Automatic container management** for SQLite persistence
- ✅ Easier to maintain and extend
- ✅ Modern .NET development practices

This uses a single .NET application that works identically across all platforms.

## 📋 Platform-Specific Scripts

### Linux/macOS (Bash)
```bash
./start-partnership-agent.sh
```

### Windows (PowerShell)
```powershell
.\start-partnership-agent.ps1
```

## 🔍 Citation Testing

### Dedicated Citation Tests
```bash
# Linux/macOS
./test-citations.sh

# Windows
.\test-citations.ps1
```

## 📁 Setup Files

| File | Purpose |
|------|---------|
| `setup.sh` / `setup.cmd` | **Cross-platform launchers** |
| `CrossPlatformSetup.cs` | **Unified .NET setup application** |
| `start-partnership-agent.sh` | Bash setup script |
| `start-partnership-agent.ps1` | PowerShell setup script |
| `test-citations.sh` | Bash citation testing |
| `test-citations.ps1` | PowerShell citation testing |
| `sample-documents-bulk.json` | Enhanced documents for citations |
| `setup-elasticsearch.json` | Elasticsearch index mapping |
| `TESTING_CITATIONS.md` | Complete testing guide |

## 💾 Chat History Provider Options

The setup supports three chat history storage options:

### 🧠 **InMemory (Default)**
- **Best for**: Development, testing, quick demos
- **Persistence**: No persistence - data lost on restart
- **Setup**: Zero configuration required
- **Performance**: Fastest startup and response times
- **Usage**: `./setup.sh` or `./setup.sh inmemory`

### 🗄️ **SQLite (Recommended for Local Development)**
- **Best for**: Local development with persistence, offline work
- **Persistence**: Full persistence in local SQLite database
- **Setup**: Automatic Docker container with volume mounting
- **Performance**: Fast local file-based storage
- **Container**: `partnership-agent-sqlite` with persistent volume
- **Usage**: `./setup.sh sqlite`

### ☁️ **Azure SQL Database**
- **Best for**: Production deployments, multi-user scenarios
- **Persistence**: Full persistence in cloud database
- **Setup**: Requires Azure SQL Database and connection configuration
- **Performance**: Network-dependent, highly scalable
- **Features**: Enterprise-grade reliability and backup
- **Usage**: `./setup.sh azuresql`

## 🎯 What Gets Set Up

1. **Elasticsearch** (Docker container)
   - Index: `partnership-documents`
   - 8 comprehensive documents with rich content
   - Enhanced mapping for citation metadata

2. **Chat History Storage** (based on provider choice)
   - **InMemory**: No additional setup required
   - **SQLite**: Docker container with persistent volume
   - **Azure SQL**: Uses existing database configuration

3. **Sample Documents** include:
   - Partnership Agreement Template
   - Revenue Sharing Guidelines
   - Compliance Requirements
   - Performance Metrics
   - Data Security Policies
   - And more...

4. **Citation Features**:
   - Document excerpts with precise positions
   - Relevance scoring
   - Context before/after excerpts
   - Multiple source references

## 💡 Recommendations

### For Cross-Platform Teams
**Use the .NET-based setup** (`setup.sh` / `setup.cmd`):
- ✅ Single codebase to maintain
- ✅ Consistent behavior across platforms
- ✅ Better error handling and logging
- ✅ Easier to extend and modify

### For Platform-Specific Workflows
**Use the shell scripts** for:
- Integration with existing bash/PowerShell workflows
- Custom modifications for specific environments
- CI/CD pipelines with shell script requirements

## 🛠️ Requirements

- **.NET 8 SDK** - Required for cross-platform setup application
- **Docker** - Docker Desktop (Windows) or Docker Engine (Linux/macOS)
  - Required for Elasticsearch (all setups)
  - Required for SQLite chat history (optional)
- **Internet connection** - For downloading Docker images
- **Azure SQL Database** - Only required if using Azure SQL chat history option

## 📝 Citation Test Prompts

The setup includes test prompts designed to generate rich citations:

1. "What are the revenue sharing percentages for different partner tiers?"
2. "What is the minimum credit score requirement for partner verification?"
3. "How long is the notice period for partnership termination?"
4. "What customer satisfaction scores are required?"
5. And 6 more targeted scenarios...

## 🔧 Troubleshooting

See `TESTING_CITATIONS.md` for:
- Complete testing guide
- Expected citation output format
- Common issues and solutions
- API verification commands

## 🔧 Configuration Options

### Command-Line Arguments
```bash
# No argument defaults to InMemory
./setup.sh

# Explicit provider selection
./setup.sh inmemory    # Fast, no persistence
./setup.sh sqlite      # Local persistence
./setup.sh azuresql    # Cloud persistence
```

### Manual .NET Execution
```bash
cd setup
dotnet run --project CrossPlatformSetup.csproj [provider]
```

### User Secrets Configuration
For Azure SQL setup, configure connection string:
```bash
cd src/PartnershipAgent.WebApi
dotnet user-secrets set "AzureSQL:ConnectionString" "your-connection-string"
```

## 🚀 Future Improvements

The .NET-based approach enables:
- ✅ **Multi-provider chat history support** (Already implemented)
- ✅ **Automatic container management** (Already implemented)
- Configuration file support
- Interactive prompts for setup options
- Automatic dependency detection
- Better integration testing
- Plugin architecture for custom setups