using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PartnershipAgent.ConsoleApp.Services;
using PartnershipAgent.Core.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Register configuration
builder.Services.Configure<WebApiConfiguration>(
    builder.Configuration.GetSection("WebApi"));

builder.Services.AddHttpClient<ChatService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WebApiConfiguration>>().Value;
    
    // Configure timeout based on settings
    if (config.TimeoutSeconds <= 0)
    {
        client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
    }
    else
    {
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    }
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    handler.MaxConnectionsPerServer = 100;
    return handler;
});
builder.Services.AddScoped<ChatService>();

var host = builder.Build();

var chatService = host.Services.GetRequiredService<ChatService>();
await chatService.RunInteractiveChat();

await host.RunAsync();