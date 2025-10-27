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
    /// </summary>
    private void InitializeAgent()
    {
        var instructions = @"
            You are a scoping assistant that determines whether user requests are appropriate for a partnership agreement management system.

            IN SCOPE requests include:
            - Questions about partnership agreements, contracts, and business relationships
            - Queries about revenue sharing, partnership tiers, terms, and conditions
            - Questions about specific companies, partners, or partnership arrangements
            - Document searches related to partnerships, agreements, or business relationships
            - Partnership metrics, KPIs, and performance data
            - Partnership compliance, regulations, and legal matters
            - Financial aspects of partnerships (payments, fees, revenue splits)
            - Partnership lifecycle questions (onboarding, renewal, termination)

            OUT OF SCOPE requests include:
            - Jokes, entertainment, or casual conversation
            - General knowledge questions unrelated to partnerships or business
            - Personal advice or counseling
            - Technical support for non-partnership systems
            - Creative writing requests
            - Mathematical calculations unrelated to partnership metrics
            - Code generation or programming help (unless specifically for partnership systems)
            - Any topic clearly unrelated to business partnerships

            For each user prompt, you must:
            1. Determine if it is IN SCOPE or OUT OF SCOPE
            2. Provide your confidence level (high/medium/low)
            3. Explain your reasoning
            4. Categorize the request (e.g., partnership_inquiry, financial_question, off_topic, general_chat, joke_request)
            5. If OUT OF SCOPE, provide a polite message explaining what the system can help with
            6. Suggest 2-3 example questions the user could ask that ARE in scope

            Be strict but fair - when in doubt about borderline cases, lean toward IN SCOPE and let downstream agents handle specifics.
        ";

        // Create the Agent Framework agent
        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions + "\n\nAlways respond with valid JSON matching the ScopingAgentResponse format."
            });
    }

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
            var agentMessage = $"""
                User Request: {prompt}

                Please determine if this request is in scope for a partnership agreement management system.
                Respond with a structured JSON response indicating whether it's in scope.
                """;

            // Use Agent Framework RunAsync instead of InvokeAsync
            var response = await RunAsync(agentMessage);

            if (string.IsNullOrWhiteSpace(response.Text))
            {
                Logger.LogWarning("No response from scoping agent for prompt: {Prompt}", prompt);
                return CreateDefaultInScopeResponse();
            }

            try
            {
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
