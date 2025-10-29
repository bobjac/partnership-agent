using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using PartnershipAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Agent Framework (v2) implementation of the EntityResolutionAgent.
/// Specialized agent that extracts entities from user input
/// using structured responses and LLM-driven analysis.
/// </summary>
public class EntityResolutionAgentV2 : BaseAgent
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// The name of the agent used for identification in the Agent Framework system.
    /// </summary>
    public override string Name => "EntityResolutionAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that extracts and analyzes entities from user input using structured responses";

    /// <summary>
    /// Constructor for the EntityResolutionAgentV2.
    /// Initializes the agent with required dependencies and configures the Agent Framework client.
    /// </summary>
    public EntityResolutionAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IRequestedBy requestedBy,
        ILogger<EntityResolutionAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatClientAgent with the appropriate settings and instructions.
    /// Configures structured JSON output for reliable entity extraction.
    /// </summary>
    private void InitializeAgent()
    {
        var instructions = @"
You are an entity extraction assistant for a partnership agreement management system.

Extract relevant entities from user questions about partnerships, contracts, and business relationships.

Entity Types to Extract:
- company: Company names, organizations, vendors
- person: Person names, roles, contacts
- partnership_term: Partnership-specific terminology
- financial: Dollar amounts, percentages, financial terms
- date: Dates, time periods, deadlines
- contract_term: Legal concepts, agreement terms
- metric: Business KPIs, performance indicators
- general: Other relevant terms

Confidence Scoring (0.0 to 1.0):
- 0.9-1.0: Highly certain (explicit company name, clear financial amount)
- 0.7-0.9: Moderately certain (likely partnership term, probable metric)
- 0.5-0.7: Uncertain (ambiguous term, requires context)
- Below 0.5: Low confidence

Extract only meaningful entities. If no significant entities exist, return an empty list.
";

        // Configure chat options with structured JSON output using type-based schema
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                AIJsonUtilities.CreateJsonSchema(typeof(EntityResolutionResponse)),
                "entity_extraction_response",
                "Entity extraction response for partnership queries"
            )
        };

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });

        _chatOptions = chatOptions;
    }

    private ChatOptions? _chatOptions;

    /// <summary>
    /// Extracts entities from the provided text using LLM analysis.
    /// </summary>
    /// <param name="prompt">The text to analyze for entity extraction</param>
    /// <returns>List of extracted entities with metadata</returns>
    public async Task<List<ExtractedEntity>> ExtractEntities(string prompt)
    {
        Logger.LogInformation("Extracting entities from prompt: {Prompt}", prompt);

        try
        {
            var agentMessage = $"Extract entities from: {prompt}";

            // Use Agent Framework RunAsync with structured output configuration
            var options = new ChatClientAgentRunOptions
            {
                ChatOptions = _chatOptions
            };

            var response = await RunAsync(agentMessage, options: options);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                Logger.LogWarning("No response from entity resolution agent for prompt: {Prompt}", prompt);
                return [];
            }

            try
            {
                // With structured output, the response is guaranteed to match the schema
                var entityResponse = JsonSerializer.Deserialize<EntityResolutionResponse>(
                    response.Text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (entityResponse == null || entityResponse.ExtractedEntities == null)
                {
                    Logger.LogWarning("Failed to deserialize entity response, returning empty list");
                    return [];
                }

                Logger.LogInformation("Extracted {Count} entities", entityResponse.ExtractedEntities.Count);
                return entityResponse.ExtractedEntities;
            }
            catch (JsonException jsonEx)
            {
                Logger.LogWarning(jsonEx, "Failed to deserialize entity response. Content: {Content}", response.Text);
                return [];
            }
        }
        catch (Exception ex) when (LogException(ex, $"Error extracting entities from prompt: {prompt}"))
        {
            return [];
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(string prompt)
    {
        return await ExtractEntities(prompt);
    }

    /// <summary>
    /// Helper method for logging exceptions with consistent format.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">Additional context message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogException(Exception ex, string message)
    {
        Logger.LogError(ex, "EntityResolutionAgent error: {Message}", message);
        return true;
    }
}
