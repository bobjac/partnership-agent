namespace PartnershipAgent.Core.Models;

public class ChatRequest
{
    public string ThreadId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}