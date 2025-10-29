using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using OpenTelemetry;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Evaluation;
using PartnershipAgent.Core.Services;
using PartnershipAgent.Core.Workflows;
using System;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel with no timeouts for debugging
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = System.Threading.Timeout.InfiniteTimeSpan;
    options.Limits.RequestHeadersTimeout = System.Threading.Timeout.InfiniteTimeSpan;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure OpenTelemetry
var applicationInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] 
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

builder.Services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddSource("PartnershipAgent.StepOrchestration")
            .AddSource("PartnershipAgent.Agents") 
            .AddSource("PartnershipAgent.Evaluation")
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: "PartnershipAgent", serviceVersion: "1.0.0"))
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) => !httpContext.Request.Path.StartsWithSegments("/health");
            });

        if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
        {
            Console.WriteLine($"[TELEMETRY] Configuring Azure Monitor with connection string: {applicationInsightsConnectionString[..50]}...");
            try 
            {
                builder.AddAzureMonitorTraceExporter(options =>
                {
                    options.ConnectionString = applicationInsightsConnectionString;
                });
                builder.AddConsoleExporter(); // Keep console for debugging
                Console.WriteLine("[TELEMETRY] Azure Monitor Trace Exporter and Console Exporter configured successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TELEMETRY] Error configuring Azure Monitor: {ex.Message}");
                builder.AddConsoleExporter(); // Fallback to console only
            }
        }
        else
        {
            Console.WriteLine("[TELEMETRY] No Application Insights connection string found, using console only");
            builder.AddConsoleExporter();
        }
    });

var azureOpenAIEndpoint = builder.Configuration["AzureOpenAI:Endpoint"] 
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Azure OpenAI endpoint not found in configuration or environment variables");

var azureOpenAIApiKey = builder.Configuration["AzureOpenAI:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("Azure OpenAI API key not found in configuration or environment variables");

var azureOpenAIDeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"] 
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-35-turbo";

var azureOpenAIApiVersion = builder.Configuration["AzureOpenAI:ApiVersion"]
    ?? "2024-08-01-preview"; // Required for structured output (json_schema)
var elasticSearchUri = builder.Configuration["ElasticSearch:Uri"] ?? "http://localhost:9200";
var elasticUsername = builder.Configuration["ElasticSearch:Username"];
var elasticPassword = builder.Configuration["ElasticSearch:Password"];


// Register IChatClient for Agent Framework (v2) agents
builder.Services.AddSingleton<IChatClient>(provider =>
{
    // For Azure OpenAI, construct the full deployment-specific endpoint with api-version
    // Format: https://{resource}.openai.azure.com/openai/deployments/{deployment}/?api-version={version}
    var baseUri = new Uri(azureOpenAIEndpoint.TrimEnd('/'));
    var azureChatEndpoint = new Uri(baseUri, $"openai/deployments/{azureOpenAIDeploymentName}/?api-version={azureOpenAIApiVersion}");

    var chatClient = new OpenAI.Chat.ChatClient(
        model: azureOpenAIDeploymentName,
        credential: new System.ClientModel.ApiKeyCredential(azureOpenAIApiKey),
        options: new OpenAI.OpenAIClientOptions()
        {
            Endpoint = azureChatEndpoint
        });
    return chatClient.AsIChatClient();
});

var settings = new ConnectionSettings(new Uri(elasticSearchUri))
    .DefaultIndex("partnership-documents")
    .DisableDirectStreaming();

if (!string.IsNullOrEmpty(elasticUsername) && !string.IsNullOrEmpty(elasticPassword))
{
    settings = settings.BasicAuthentication(elasticUsername, elasticPassword);
}

builder.Services.AddSingleton<IElasticClient>(new ElasticClient(settings));

// ============================================================================
// Agent Framework (v2) Agents
// ============================================================================

builder.Services.AddScoped<ScopingAgentV2>(provider =>
{
    var chatClient = provider.GetRequiredService<IChatClient>();
    var logger = provider.GetRequiredService<ILogger<ScopingAgentV2>>();
    var requestedBy = new SimpleRequestedBy();
    var threadId = Guid.NewGuid();

    return new ScopingAgentV2(threadId, chatClient, requestedBy, logger);
});

builder.Services.AddScoped<EntityResolutionAgentV2>(provider =>
{
    var chatClient = provider.GetRequiredService<IChatClient>();
    var logger = provider.GetRequiredService<ILogger<EntityResolutionAgentV2>>();
    var requestedBy = new SimpleRequestedBy();
    var threadId = Guid.NewGuid();

    return new EntityResolutionAgentV2(threadId, chatClient, requestedBy, logger);
});

builder.Services.AddScoped<DocumentSearchAgentV2>(provider =>
{
    var chatClient = provider.GetRequiredService<IChatClient>();
    var elasticSearchService = provider.GetRequiredService<IElasticSearchService>();
    var logger = provider.GetRequiredService<ILogger<DocumentSearchAgentV2>>();
    var requestedBy = new SimpleRequestedBy();
    var threadId = Guid.NewGuid();

    return new DocumentSearchAgentV2(threadId, chatClient, elasticSearchService, requestedBy, logger);
});

builder.Services.AddScoped<ResponseGenerationAgentV2>(provider =>
{
    var chatClient = provider.GetRequiredService<IChatClient>();
    var citationService = provider.GetRequiredService<ICitationService>();
    var chatHistoryService = provider.GetRequiredService<IChatHistoryService>();
    var logger = provider.GetRequiredService<ILogger<ResponseGenerationAgentV2>>();
    var requestedBy = new SimpleRequestedBy();
    var threadId = Guid.NewGuid();

    return new ResponseGenerationAgentV2(threadId, chatClient, citationService, chatHistoryService, requestedBy, logger);
});

// ============================================================================

builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddScoped<ICitationService, CitationService>();

// Configure Chat History Service based on provider
var chatHistoryProvider = builder.Configuration["ChatHistory:Provider"] ?? "InMemory";

switch (chatHistoryProvider.ToLowerInvariant())
{
    case "azuresql":
        var azureSQLConnectionString = builder.Configuration["AzureSQL:ConnectionString"] 
            ?? Environment.GetEnvironmentVariable("AZURESQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Azure SQL Connection String not found in configuration or environment variables");
        
        builder.Services.AddSingleton<ISqlConnectionFactory>(sp => new SqlConnectionFactory(azureSQLConnectionString));
        builder.Services.AddScoped<IChatHistoryService, AzureSqlChatHistoryService>();
        Console.WriteLine("[CHAT HISTORY] Using Azure SQL provider");
        break;
        
    case "sqlite":
        var sqliteConnectionString = builder.Configuration["SQLite:ConnectionString"] 
            ?? Environment.GetEnvironmentVariable("SQLITE_CONNECTION_STRING")
            ?? "Data Source=/data/partnership-agent.db;Cache=Shared";
        
        builder.Services.AddSingleton<ISqlConnectionFactory>(sp => new SqliteConnectionFactory(sqliteConnectionString));
        builder.Services.AddScoped<IChatHistoryService, SqliteChatHistoryService>();
        Console.WriteLine($"[CHAT HISTORY] Using SQLite provider with connection: {sqliteConnectionString}");
        break;
        
    case "inmemory":
    default:
        builder.Services.AddScoped<IChatHistoryService, InMemoryChatHistoryService>();
        Console.WriteLine("[CHAT HISTORY] Using InMemory provider");
        break;
}

// Register vector search services
builder.Services.AddScoped<IVectorSearchService, AzureVectorSearchService>();
builder.Services.AddScoped<DocumentIndexingService>();

// Register search service based on configuration
var useVectorSearch = builder.Configuration.GetValue<bool>("AzureSearch:UseVectorSearch", false);
if (useVectorSearch)
{
    // Use vector search directly to avoid circular dependency
    builder.Services.AddScoped<IElasticSearchService>(sp => 
    {
        var vectorSearchService = sp.GetRequiredService<IVectorSearchService>();
        var logger = sp.GetRequiredService<ILogger<VectorSearchAdapter>>();
        return new VectorSearchAdapter(vectorSearchService, logger);
    });
    Console.WriteLine("[SEARCH] Using high-performance Vector Search (Azure AI Search)");
}
else
{
    builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
    Console.WriteLine("[SEARCH] Using traditional Elasticsearch");
}

// Register the workflow service using Agent Framework's formal WorkflowBuilder pattern
builder.Services.AddScoped<PartnershipWorkflowService>();

// Register ground truth service
builder.Services.AddSingleton<IGroundTruthService, GroundTruthService>();

// Register evaluation services conditionally
var evaluationEnabled = builder.Configuration.GetValue<bool>("Evaluation:Enabled", false);
if (evaluationEnabled)
{
    builder.Services.AddScoped<IAssistantResponseEvaluator, AssistantResponseEvaluator>();
}


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>
/// Simple implementation of IRequestedBy for the web API context.
/// </summary>
public class SimpleRequestedBy : IRequestedBy
{
    public string UserId { get; set; } = "mock-user-123";
    public string CompanyId { get; set; } = "company-123";
    public string CompanyName { get; set; } = "Default Company";
    public string ProjectId { get; set; } = "project-123";
}