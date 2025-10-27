using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Base class for Agent Framework agents that provides common functionality and properties.
/// This is the Agent Framework (v2) equivalent of BaseChatHistoryAgent (Semantic Kernel v1).
/// </summary>
public abstract class BaseAgent
{
    protected readonly ILogger Logger;
    protected readonly Guid ThreadId;
    protected readonly IRequestedBy RequestedBy;

    /// <summary>
    /// The underlying Agent Framework ChatClientAgent instance.
    /// </summary>
    public ChatClientAgent? Agent { get; protected set; }

    /// <summary>
    /// The name of the agent used for identification in the Agent Framework system.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Initializes a new instance of the BaseAgent class.
    /// </summary>
    /// <param name="requestedBy">The user context for the agent session</param>
    /// <param name="threadId">Unique identifier for the chat session</param>
    /// <param name="logger">Logger instance for the agent</param>
    protected BaseAgent(IRequestedBy requestedBy, Guid threadId, ILogger logger)
    {
        RequestedBy = requestedBy ?? throw new ArgumentNullException(nameof(requestedBy));
        ThreadId = threadId;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs an exception and returns true for use in catch clauses with when conditions.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">Additional message to include in the log</param>
    /// <returns>Always returns true to allow the catch block to execute</returns>
    protected bool Log(Exception ex, string message)
    {
        Logger.LogError(ex, message);
        return true;
    }

    /// <summary>
    /// Runs the agent with the provided input and returns the response.
    /// </summary>
    /// <param name="input">The user input to process</param>
    /// <param name="thread">Optional agent thread for conversation context</param>
    /// <param name="options">Optional agent run options</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The agent's response</returns>
    public virtual async Task<AgentRunResponse> RunAsync(
        string input,
        AgentThread? thread = null,
        ChatClientAgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (Agent == null)
        {
            throw new InvalidOperationException("Agent not initialized");
        }

        Logger.LogInformation("Running agent {AgentName} with ThreadId: {ThreadId}", Name, ThreadId);

        try
        {
            return await Agent.RunAsync(input, thread, options, cancellationToken);
        }
        catch (Exception ex) when (Log(ex, $"Error running agent {Name}"))
        {
            throw;
        }
    }

    /// <summary>
    /// Runs the agent with streaming responses.
    /// </summary>
    /// <param name="input">The user input to process</param>
    /// <param name="thread">Optional agent thread for conversation context</param>
    /// <param name="options">Optional agent run options</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>An asynchronous enumerable of streaming response updates</returns>
    public virtual IAsyncEnumerable<AgentRunResponseUpdate> RunStreamingAsync(
        string input,
        AgentThread? thread = null,
        ChatClientAgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (Agent == null)
        {
            throw new InvalidOperationException("Agent not initialized");
        }

        Logger.LogInformation("Running agent {AgentName} (streaming) with ThreadId: {ThreadId}", Name, ThreadId);

        return Agent.RunStreamingAsync(input, thread, options, cancellationToken);
    }
}
