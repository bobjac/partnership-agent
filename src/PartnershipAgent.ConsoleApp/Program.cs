using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartnershipAgent.ConsoleApp.Services;
using PartnershipAgent.Core.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Register configuration
builder.Services.Configure<WebApiConfiguration>(
    builder.Configuration.GetSection("WebApi"));

// Register Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetryWorkerService(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

//Register Application Insights Logging
builder.Logging.AddApplicationInsights(
    configureTelemetryConfiguration: (config) =>
    {
        config.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    },
    configureApplicationInsightsLoggerOptions: (options) => { }
);

// Configure console logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);


builder.Services.AddHttpClient<ChatService>((serviceProvider, client) =>
{
    // Force infinite timeout for debugging - no timeouts anywhere
    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    handler.MaxConnectionsPerServer = 100;
    // Remove any potential socket timeouts
    return handler;
});
builder.Services.AddScoped<ChatService>();

var host = builder.Build();

var chatService = host.Services.GetRequiredService<ChatService>();
await chatService.RunInteractiveChat();

await host.RunAsync();