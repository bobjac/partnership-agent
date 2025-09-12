# Azure Key Vault Integration

## 1. Install Package
```bash
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

## 2. Update Program.cs
```csharp
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Key Vault configuration
if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
    if (!string.IsNullOrEmpty(keyVaultEndpoint))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential());
    }
}
```

## 3. Store Secrets in Key Vault
```bash
# Create secrets in Key Vault
az keyvault secret set --vault-name "your-keyvault-name" --name "AzureOpenAI--Endpoint" --value "https://your-resource.openai.azure.com/"
az keyvault secret set --vault-name "your-keyvault-name" --name "AzureOpenAI--ApiKey" --value "your-api-key"
az keyvault secret set --vault-name "your-keyvault-name" --name "AzureOpenAI--DeploymentName" --value "gpt-35-turbo"
```

## 4. Update appsettings.Production.json
```json
{
  "KeyVault": {
    "Endpoint": "https://your-keyvault-name.vault.azure.net/"
  }
}
```