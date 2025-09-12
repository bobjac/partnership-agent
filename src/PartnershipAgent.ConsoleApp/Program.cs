using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PartnershipAgent.ConsoleApp.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<ChatService>().ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    return handler;
});
builder.Services.AddScoped<ChatService>();

var host = builder.Build();

var chatService = host.Services.GetRequiredService<ChatService>();
await chatService.RunInteractiveChat();

await host.RunAsync();