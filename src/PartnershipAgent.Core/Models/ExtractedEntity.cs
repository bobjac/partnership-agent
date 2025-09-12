namespace PartnershipAgent.Core.Models;

public class ExtractedEntity
{
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Confidence { get; set; }
}