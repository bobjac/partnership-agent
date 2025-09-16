# Partnership Agent Setup Scripts

This directory contains multiple setup options for the Partnership Agent with enhanced citation functionality.

## 🚀 Quick Start (Recommended)

### ⭐ **Cross-Platform Setup (PREFERRED)**
**Works on Windows, macOS, and Linux**

```bash
# Linux/macOS
./setup.sh

# Windows Command Prompt
setup.cmd

# Windows PowerShell
.\setup.cmd
```

**Why this is preferred:**
- ✅ Single codebase - consistent across all platforms
- ✅ Better error handling and professional logging
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

## 🎯 What Gets Set Up

1. **Elasticsearch** (Docker container)
   - Index: `partnership-documents`
   - 8 comprehensive documents with rich content
   - Enhanced mapping for citation metadata

2. **Sample Documents** include:
   - Partnership Agreement Template
   - Revenue Sharing Guidelines
   - Compliance Requirements
   - Performance Metrics
   - Data Security Policies
   - And more...

3. **Citation Features**:
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

- .NET 8 SDK
- Docker (Docker Desktop on Windows)
- Internet connection (for downloading Elasticsearch image)

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

## 🚀 Future Improvements

The .NET-based approach enables:
- Configuration file support
- Interactive prompts for setup options
- Automatic dependency detection
- Better integration testing
- Plugin architecture for custom setups