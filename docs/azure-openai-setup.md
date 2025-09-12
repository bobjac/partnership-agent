# Azure OpenAI Setup Guide

## 1. Create Azure OpenAI Resource

1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource"
3. Search for "Azure OpenAI"
4. Click "Create" and fill in the required details:
   - **Subscription**: Select your subscription
   - **Resource Group**: Create new or use existing
   - **Region**: Choose a region that supports Azure OpenAI (e.g., East US, West Europe)
   - **Name**: Choose a unique name for your resource
   - **Pricing Tier**: Select Standard S0

## 2. Deploy a Model

1. Once the resource is created, go to the resource in Azure Portal
2. Click on "Model deployments" in the left menu
3. Click "Create new deployment"
4. Configure the deployment:
   - **Model**: Select `gpt-35-turbo` or `gpt-4` (recommended: gpt-35-turbo for cost efficiency)
   - **Deployment name**: Use `gpt-35-turbo` (this will be your DeploymentName)
   - **Model version**: Use the latest available version
   - **Deployment type**: Standard
   - **Tokens per minute rate limit**: Set based on your needs (e.g., 30K for development)

## 3. Get Configuration Values

After deployment, you'll need these values for your application:

### From Azure Portal:
1. **Endpoint**: Go to "Keys and Endpoint" → Copy the "Endpoint" value
   - Format: `https://your-resource-name.openai.azure.com/`
2. **API Key**: Copy "Key 1" or "Key 2"
3. **Deployment Name**: The name you gave your model deployment (e.g., `gpt-35-turbo`)

### Set Environment Variables:
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-32-character-api-key-here"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-35-turbo"
```

## 4. Supported Models

Common deployment names and their use cases:
- **gpt-35-turbo**: Cost-effective, good for most applications
- **gpt-4**: More capable but more expensive
- **gpt-4-turbo**: Latest GPT-4 variant with larger context window

## 5. Cost Considerations

- **GPT-3.5-turbo**: ~$0.0015 per 1K input tokens, ~$0.002 per 1K output tokens
- **GPT-4**: ~$0.03 per 1K input tokens, ~$0.06 per 1K output tokens

For development and testing, GPT-3.5-turbo is recommended.

## 6. Regional Availability

Azure OpenAI is available in select regions. Check the [official documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/concepts/models#model-summary-table-and-region-availability) for current availability.

## 7. Testing Your Setup

Once configured, test your setup by running:

```bash
cd src/PartnershipAgent.WebApi
dotnet run
```

Then test the health endpoint:
```bash
curl -k https://localhost:7001/api/chat/health
```

If configured correctly, the EntityResolution agent should now work with real Azure OpenAI responses instead of returning errors.