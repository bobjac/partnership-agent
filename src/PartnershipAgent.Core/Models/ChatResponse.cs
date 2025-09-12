using System.Collections.Generic;

namespace PartnershipAgent.Core.Models;

public class ChatResponse
{
    public string ThreadId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<string> ExtractedEntities { get; set; } = new();
    public List<DocumentResult> RelevantDocuments { get; set; } = new();
}