using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PartnershipAgent.Core.Models;

public class DocumentCitation
{
    [JsonPropertyName("document_id")]
    [Description("Unique identifier of the source document")]
    public required string DocumentId { get; set; }

    [JsonPropertyName("document_title")]
    [Description("Title of the source document")]
    public required string DocumentTitle { get; set; }

    [JsonPropertyName("category")]
    [Description("Category of the source document")]
    public required string Category { get; set; }

    [JsonPropertyName("excerpt")]
    [Description("Relevant text excerpt from the document that supports the answer")]
    public required string Excerpt { get; set; }

    [JsonPropertyName("start_position")]
    [Description("Character position where the excerpt begins in the document")]
    public int StartPosition { get; set; }

    [JsonPropertyName("end_position")]
    [Description("Character position where the excerpt ends in the document")]
    public int EndPosition { get; set; }

    [JsonPropertyName("relevance_score")]
    [Description("Relevance score of this citation to the user's query (0.0 to 1.0)")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("context_before")]
    [Description("Brief context text that appears before the excerpt")]
    public string ContextBefore { get; set; } = string.Empty;

    [JsonPropertyName("context_after")]
    [Description("Brief context text that appears after the excerpt")]
    public string ContextAfter { get; set; } = string.Empty;
}