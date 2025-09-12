using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PartnershipAgent.Core.Models;

public class FAQAgentResponse
{
    [JsonPropertyName("answer")]
    [Description("The main answer to the user's question")]
    public required string Answer { get; set; }

    [JsonPropertyName("confidence_level")]
    [Description("Confidence level in the answer: high, medium, or low")]
    public required string ConfidenceLevel { get; set; }

    [JsonPropertyName("source_documents")]
    [Description("List of document titles that were used to generate the answer")]
    public List<string> SourceDocuments { get; set; } = [];

    [JsonPropertyName("has_complete_answer")]
    [Description("Whether the provided documents contain enough information to fully answer the question")]
    public bool HasCompleteAnswer { get; set; }

    [JsonPropertyName("follow_up_suggestions")]
    [Description("List of suggested follow-up questions related to the topic")]
    public List<string> FollowUpSuggestions { get; set; } = [];
}