namespace PartnershipAgent.Core.Configuration;

/// <summary>
/// Configuration for Web API connection settings
/// </summary>
public class WebApiConfiguration
{
    /// <summary>
    /// The base URL for the Web API (e.g., "http://localhost:5000")
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// API endpoint paths
    /// </summary>
    public WebApiEndpoints Endpoints { get; set; } = new();

    /// <summary>
    /// Gets the full URL for the chat endpoint
    /// </summary>
    public string ChatUrl => $"{BaseUrl.TrimEnd('/')}{Endpoints.Chat}";

    /// <summary>
    /// Gets the full URL for the health endpoint
    /// </summary>
    public string HealthUrl => $"{BaseUrl.TrimEnd('/')}{Endpoints.Health}";
}

/// <summary>
/// Web API endpoint paths
/// </summary>
public class WebApiEndpoints
{
    /// <summary>
    /// Chat endpoint path
    /// </summary>
    public string Chat { get; set; } = "/api/chat";

    /// <summary>
    /// Health check endpoint path
    /// </summary>
    public string Health { get; set; } = "/api/chat/health";
}