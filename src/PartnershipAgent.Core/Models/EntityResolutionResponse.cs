using System.Collections.Generic;

namespace PartnershipAgent.Core.Models;

/// <summary>
/// Response model for entity resolution operations containing extracted entities and analysis metadata.
/// </summary>
public class EntityResolutionResponse
{
    /// <summary>
    /// The original prompt that was analyzed for entity extraction.
    /// </summary>
    public string AnalyzedPrompt { get; set; } = string.Empty;

    /// <summary>
    /// List of entities extracted from the prompt.
    /// </summary>
    public List<ExtractedEntity> ExtractedEntities { get; set; } = new();

    /// <summary>
    /// Confidence level in the entity extraction quality (high/medium/low).
    /// </summary>
    public string ConfidenceLevel { get; set; } = "medium";

    /// <summary>
    /// Whether the extraction was successful and found meaningful entities.
    /// </summary>
    public bool HasMeaningfulEntities { get; set; } = true;

    /// <summary>
    /// Summary of the types of entities found.
    /// </summary>
    public string EntitySummary { get; set; } = string.Empty;

    /// <summary>
    /// Suggested improvements or clarifications for better entity extraction.
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
}