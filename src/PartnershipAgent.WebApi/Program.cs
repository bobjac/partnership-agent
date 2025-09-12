using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Nest;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddScoped(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: azureOpenAIDeploymentName,
        endpoint: azureOpenAIEndpoint,
        apiKey: azureOpenAIApiKey);
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

builder.Services.AddScoped<IEntityResolutionAgent, EntityResolutionAgent>();
builder.Services.AddScoped<IFAQAgent, FAQAgent>();
builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
builder.Services.AddScoped<SimpleChatProcessService>();

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