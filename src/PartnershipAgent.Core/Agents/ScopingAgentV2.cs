using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using PartnershipAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Agent Framework (v2) implementation of the ScopingAgent.
/// Determines whether a user's request is within the scope of the
/// partnership agent system. Filters out off-topic requests, jokes, and other
/// non-partnership-related queries.
/// </summary>
public class ScopingAgentV2 : BaseAgent, IScopingAgent
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// The name of the agent used for identification in the Agent Framework system.
    /// </summary>
    public override string Name => "ScopingAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that determines whether user requests are in scope for the partnership agent system";

    /// <summary>
    /// Constructor for the ScopingAgentV2.
    /// Initializes the agent with required dependencies and configures the Agent Framework client.
    /// </summary>
    public ScopingAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IRequestedBy requestedBy,
        ILogger<ScopingAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatClientAgent with the appropriate settings and instructions.
    /// Configures structured JSON output for reliable scoping decisions.
    /// </summary>
    private void InitializeAgent()
    {
        var instructions = @"
You are a scoping assistant for a partnership agreement management system.

Classify user questions as IN SCOPE or OUT OF SCOPE:

IN SCOPE - Questions about:
- Partnerships, agreements, contracts, business relationships
- Revenue, payments, tiers, fees, pricing, commissions
- Partners, companies, vendors
- Terms, conditions, compliance, regulations
- Partnership lifecycle (onboarding, renewal, termination)

OUT OF SCOPE - Questions about:
- Jokes, entertainment, casual conversation
- Weather, sports, general trivia
- Personal advice, creative writing
- Unrelated technical support

CRITICAL: If the question mentions 'partnership', 'revenue', 'tier', 'agreement', 'contract', 'payment', 'partner', or 'company', mark isInScope=true.

For out-of-scope requests, provide a helpful outOfScopeMessage and suggest 2-3 example questions.
";

        // Configure chat options with structured JSON output using type-based schema
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                AIJsonUtilities.CreateJsonSchema(typeof(ScopingAgentResponse)),
                "scoping_response",
                "Scoping response for partnership agent requests"
            )
        };

        // Create the Agent Framework agent with structured output
        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });

        // Store chat options to be used in RunAsync
        _chatOptions = chatOptions;
    }

    private ChatOptions? _chatOptions;

    /// <summary>
    /// Evaluates whether a user prompt is in scope for the partnership agent.
    /// </summary>
    /// <param name="prompt">The user's input prompt</param>
    /// <returns>A ScopingAgentResponse indicating whether the request is in scope</returns>
    public async Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt)
    {
        Logger.LogInformation("Evaluating scope for prompt: {Prompt}", prompt);

        try
        {
            var agentMessage = $"User Request: {prompt}";

            // Use Agent Framework RunAsync with structured output configuration
            var options = new ChatClientAgentRunOptions
            {
                ChatOptions = _chatOptions
            };

            var response = await RunAsync(agentMessage, options: options);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                Logger.LogWarning("No response from scoping agent for prompt: {Prompt}", prompt);
                return CreateDefaultInScopeResponse();
            }

            try
            {
                // With structured output, the response is guaranteed to match the schema
                var scopingResponse = JsonSerializer.Deserialize<ScopingAgentResponse>(
                    response.Text,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (scopingResponse == null)
                {
                    Logger.LogWarning("Failed to deserialize scoping response, defaulting to in-scope");
                    return CreateDefaultInScopeResponse();
                }

                Logger.LogInformation("Scope evaluation complete: IsInScope={IsInScope}, Category={Category}",
                    scopingResponse.IsInScope, scopingResponse.Category);

                return scopingResponse;
            }
            catch (JsonException jsonEx)
            {
                Logger.LogWarning(jsonEx, "Failed to deserialize scoping response. Content: {Content}", response.Text);
                return CreateDefaultInScopeResponse();
            }
        }
        catch (Exception ex) when (LogException(ex, $"Error evaluating scope for prompt: {prompt}"))
        {
            // Default to in-scope on error to avoid blocking legitimate requests
            return CreateDefaultInScopeResponse();
        }
    }

    /// <summary>
    /// Creates a default in-scope response when scoping evaluation fails.
    /// This ensures legitimate requests aren't blocked by scoping errors.
    /// </summary>
    private static ScopingAgentResponse CreateDefaultInScopeResponse()
    {
        return new ScopingAgentResponse
        {
            IsInScope = true,
            ConfidenceLevel = "low",
            Reasoning = "Defaulting to in-scope due to evaluation error",
            Category = "unknown"
        };
    }

    /// <summary>
    /// Helper method for logging exceptions with consistent format.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">Additional context message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogException(Exception ex, string message)
    {
        Logger.LogError(ex, "ScopingAgent error: {Message}", message);
        return true;
    }
}
