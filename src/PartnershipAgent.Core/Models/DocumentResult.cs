namespace PartnershipAgent.Core.Models;

public class DocumentResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string TenantId { get; set; } = string.Empty;
}