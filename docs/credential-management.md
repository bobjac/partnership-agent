# Credential Management Best Practices

## ⚠️ NEVER store credentials in source control!

## 1. **User Secrets (Development) - RECOMMENDED**

Perfect for local development. Credentials are stored outside your project directory.

### Setup:
```bash
cd src/PartnershipAgent.WebApi
dotnet user-secrets init

# Set Azure OpenAI credentials
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-35-turbo"

# Set ElasticSearch credentials
dotnet user-secrets set "ElasticSearch:Username" "your-username"
dotnet user-secrets set "ElasticSearch:Password" "your-password"
dotnet user-secrets set "ElasticSearch:Uri" "https://your-elastic-cluster.com:9243"
```

### View stored secrets:
```bash
dotnet user-secrets list
```

### Remove a secret:
```bash
dotnet user-secrets remove "AzureOpenAI:ApiKey"
```

### Clear all secrets:
```bash
dotnet user-secrets clear
```

**✅ Pros:**
- Automatically excluded from source control
- Easy to use during development
- IDE integration (Visual Studio)
- Persists across builds

**❌ Cons:**
- Only works in Development environment
- Local to your machine only

---

## 2. **Environment Variables (CI/CD/Production)**

Best for production and CI/CD environments.

### Setup:
```bash
# Set environment variables
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-35-turbo"
export ELASTICSEARCH_USERNAME="your-username"
export ELASTICSEARCH_PASSWORD="your-password"
```

### For Windows:
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
set AZURE_OPENAI_API_KEY=your-api-key
```

**✅ Pros:**
- Works in any environment
- Standard practice for containerized applications
- Supported by most CI/CD platforms

**❌ Cons:**
- Can be visible in process lists
- Need to set on every machine/environment

---

## 3. **Azure Key Vault (Production) - MOST SECURE**

Enterprise-grade secret management for production applications.

### Setup:
1. Install package:
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

2. Update Program.cs:
```csharp
if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultEndpoint), 
        new DefaultAzureCredential());
}
```

3. Store secrets:
```bash
az keyvault secret set --vault-name "your-vault" --name "AzureOpenAI--Endpoint" --value "https://your-resource.openai.azure.com/"
az keyvault secret set --vault-name "your-vault" --name "AzureOpenAI--ApiKey" --value "your-api-key"
```

**✅ Pros:**
- Enterprise security
- Audit logs
- Access policies
- Automatic rotation

**❌ Cons:**
- More complex setup
- Additional Azure costs
- Requires Azure infrastructure

---

## 4. **.env Files (AVOID in Production)**

Only use for local development if User Secrets aren't available.

### Setup:
Create `.env` file in project root:
```env
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-35-turbo
```

**❌ IMPORTANT:** Never commit .env files! They're already in .gitignore.

---

## Security Checklist

- [ ] ✅ `.gitignore` includes `*.env`, `appsettings.*.json`, `secrets.json`
- [ ] ✅ User Secrets configured for development
- [ ] ✅ Environment variables used for production
- [ ] ✅ Real credentials removed from `appsettings.json`
- [ ] ✅ No credentials in source control history
- [ ] ✅ Azure Key Vault for enterprise environments

## Current Project Status

✅ **User Secrets initialized** - Ready for development
✅ **Environment variables supported** - Ready for production  
✅ **Sensitive files in .gitignore** - Protected from commits
✅ **appsettings.json cleaned** - No credentials in source

## Quick Start

1. **For Development:**
   ```bash
   cd src/PartnershipAgent.WebApi
   dotnet user-secrets set "AzureOpenAI:Endpoint" "your-endpoint"
   dotnet user-secrets set "AzureOpenAI:ApiKey" "your-key"
   dotnet user-secrets set "AzureOpenAI:DeploymentName" "your-deployment"
   dotnet run
   ```

2. **For Production:**
   ```bash
   export AZURE_OPENAI_ENDPOINT="your-endpoint"
   export AZURE_OPENAI_API_KEY="your-key"
   export AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment"
   dotnet run
   ```