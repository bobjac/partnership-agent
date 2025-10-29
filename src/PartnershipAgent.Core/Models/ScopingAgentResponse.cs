using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PartnershipAgent.Core.Models;

/// <summary>
/// Response from the ScopingAgent indicating whether a user request is in scope
/// for the partnership agent system.
/// </summary>
public class ScopingAgentResponse
{
    /// <summary>
    /// Indicates whether the user's request is in scope for the partnership agent.
    /// CRITICAL: Set to true if the question mentions partnership, revenue, tier, agreement, contract, payment, partner, company, vendor, pricing, fees, commissions, compliance, regulations, terms, conditions, or onboarding.
    /// Set to false only for clearly unrelated topics like jokes, weather, sports, entertainment.
    /// </summary>
    [JsonPropertyName("isInScope")]
    public bool IsInScope { get; set; }

    /// <summary>
    /// The confidence level of the scoping decision (high/medium/low).
    /// </summary>
    [JsonPropertyName("confidenceLevel")]
    public string ConfidenceLevel { get; set; } = "medium";

    /// <summary>
    /// Explanation of why the request is in or out of scope.
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// The category of the request (e.g., partnership_inquiry, off_topic, general_chat).
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// A user-friendly message to display when the request is out of scope.
    /// </summary>
    [JsonPropertyName("outOfScopeMessage")]
    public string? OutOfScopeMessage { get; set; }

    /// <summary>
    /// Suggested topics or examples of what the agent can help with.
    /// </summary>
    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];
}
