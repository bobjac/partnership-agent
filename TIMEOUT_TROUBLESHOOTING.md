# HttpClient Timeout Troubleshooting Guide

If you're still seeing "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing" error, follow these steps:

## 1. Verify Configuration Files

Check that your `appsettings.json` contains:
```json
{
  "WebApi": {
    "BaseUrl": "http://localhost:5000",
    "TimeoutSeconds": 0,
    "Endpoints": {
      "Chat": "/api/chat",
      "Health": "/api/chat/health"
    }
  }
}
```

## 2. Clean Build and Run

```bash
# Clean and rebuild everything
dotnet clean
dotnet build

# Run from the console app directory
cd src/PartnershipAgent.ConsoleApp
dotnet run
```

## 3. Manual HttpClient Configuration (Fallback)

If the configuration-based approach isn't working, you can manually set the timeout in `ChatService.cs`:

```csharp
public ChatService(HttpClient httpClient, IOptions<WebApiConfiguration> webApiConfig)
{
    _httpClient = httpClient;
    _apiBaseUrl = webApiConfig.Value.ChatUrl;
    
    // Force infinite timeout as fallback
    _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
}
```

## 4. Alternative: Use CancellationToken

Replace the timeout approach with a cancellation token:

```csharp
private async Task SendChatMessage(string prompt)
{
    try
    {
        var request = new ChatRequest { ThreadId = _threadId, Prompt = prompt };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Console.WriteLine("Sending request to API...");
        
        // Use a very long timeout or no timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30 minute timeout
        var response = await _httpClient.PostAsync(_apiBaseUrl, content, cts.Token);
        
        // ... rest of the method
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Request timed out. The operation is taking longer than expected.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending message: {ex.Message}");
    }
}
```

## 5. Check for Multiple HttpClient Instances

Make sure you're not accidentally creating additional HttpClient instances that bypass the configuration.

## 6. Environment Variables (Alternative)

You can also set timeout via environment variable:
```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORTDEFAULTVALUEIS_1=false
```

## 7. Verification

The console should show "HttpClient timeout set to: Infinite" when the app starts if the configuration is working correctly.