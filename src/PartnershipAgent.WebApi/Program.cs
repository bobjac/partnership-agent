using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Nest;
using OpenTelemetry;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Evaluation;
using PartnershipAgent.Core.Services;
using PartnershipAgent.Core.Steps;
using System;
using System.Net.Http;
using Microsoft.Extensions.AI;
using OpenAI;
using Azure.AI.OpenAI;
using Azure;
using Azure.Core;
using System.ClientModel;

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
    ?? "2024-02-15-preview";
var elasticSearchUri = builder.Configuration["ElasticSearch:Uri"] ?? "http://localhost:9200";
var elasticUsername = builder.Configuration["ElasticSearch:Username"];
var elasticPassword = builder.Configuration["ElasticSearch:Password"];

var azureSQLConnectionString = builder.Configuration.GetConnectionString("AzureSQL")
    ?? Environment.GetEnvironmentVariable("AzureSQL")
    ?? throw new InvalidOperationException("Azure SQL Connection String not found in configuration or environment variables");

builder.Services.AddScoped<IKernelBuilder>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Configure Azure OpenAI with NO timeout for debugging
    var httpClient = new HttpClient();
    httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan; // No timeout for debugging with breakpoints
    
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: azureOpenAIDeploymentName,
        endpoint: azureOpenAIEndpoint,
        apiKey: azureOpenAIApiKey,
        httpClient: httpClient);
    
    // Register IChatClient for evaluation framework using AsIChatClient extension method
    kernelBuilder.Services.AddSingleton<IChatClient>(provider =>
    {
        var azureClient = new AzureOpenAIClient(new Uri(azureOpenAIEndpoint), new AzureKeyCredential(azureOpenAIApiKey));
        var openAIChatClient = azureClient.GetChatClient(azureOpenAIDeploymentName);
        return openAIChatClient.AsIChatClient();
    });
    
    return kernelBuilder;
});

builder.Services.AddScoped(provider =>
{
    var kernelBuilder = provider.GetRequiredService<IKernelBuilder>();
    return kernelBuilder.Build();
});

var settings = new ConnectionSettings(new Uri(elasticSearchUri))
    .DefaultIndex("partnership-documents")
    .DisableDirectStreaming();

if (!string.IsNullOrEmpty(elasticUsername) && !string.IsNullOrEmpty(elasticPassword))
{
    settings = settings.BasicAuthentication(elasticUsername, elasticPassword);
}

builder.Services.AddSingleton<IElasticClient>(new ElasticClient(settings));

builder.Services.AddScoped<EntityResolutionAgent>(provider =>
{
    var kernelBuilder = provider.GetRequiredService<IKernelBuilder>();
    var logger = provider.GetRequiredService<ILogger<EntityResolutionAgent>>();
    
    // Create a simple IRequestedBy implementation for this context
    var requestedBy = new SimpleRequestedBy();
    var ThreadId = Guid.NewGuid();
    
    return new EntityResolutionAgent(ThreadId, kernelBuilder, requestedBy, logger);
});

builder.Services.AddScoped<FAQAgent>(provider =>
{
    var kernelBuilder = provider.GetRequiredService<IKernelBuilder>();
    var elasticSearchService = provider.GetRequiredService<IElasticSearchService>();
    var citationService = provider.GetRequiredService<ICitationService>();
    var chatHistoryService = provider.GetRequiredService<IChatHistoryService>();
    var logger = provider.GetRequiredService<ILogger<FAQAgent>>();
    
    // Create a simple IRequestedBy implementation for this context
    var requestedBy = new SimpleRequestedBy();
    var ThreadId = Guid.NewGuid();
    
    return new FAQAgent(ThreadId, kernelBuilder, elasticSearchService, citationService, chatHistoryService, requestedBy, logger);
});
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddScoped<ICitationService, CitationService>();

// Register SQL Connection Factory Service
builder.Services.AddSingleton<ISqlConnectionFactory>(sp =>
{
    return new SqlConnectionFactory(azureSQLConnectionString);
}); ;

// Register Chat History Service
builder.Services.AddScoped<IChatHistoryService, AzureSqlChatHistoryService>();

// Register the response channel
builder.Services.AddScoped<IBidirectionalToClientChannel, SimpleBidirectionalChannel>();

// Register the process response collector
builder.Services.AddSingleton<ProcessResponseCollector>();

// Register the individual step classes
builder.Services.AddScoped<EntityResolutionStep>();
builder.Services.AddScoped<DocumentSearchStep>();
builder.Services.AddScoped<ResponseGenerationStep>();
builder.Services.AddScoped<UserResponseStep>();

// Register the step orchestration service
builder.Services.AddScoped<StepOrchestrationService>();

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