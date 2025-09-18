using System;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Base class for chat history agents that provides common functionality and properties.
/// </summary>
public abstract class BaseChatHistoryAgent
{
    protected readonly ILogger Logger;
    protected readonly Guid ThreadId;
    protected readonly IRequestedBy RequestedBy;

    /// <summary>
    /// The underlying SemanticKernel ChatCompletionAgent instance.
    /// </summary>
    public ChatCompletionAgent? Agent { get; protected set; }

    /// <summary>
    /// The name of the agent used for identification in the semantic kernel system.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Initializes a new instance of the BaseChatHistoryAgent class.
    /// </summary>
    /// <param name="requestedBy">The user context for the agent session</param>
    /// <param name="threadId">Unique identifier for the chat session</param>
    /// <param name="logger">Logger instance for the agent</param>
    protected BaseChatHistoryAgent(IRequestedBy requestedBy, Guid threadId, ILogger logger)
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
}

/// <summary>
/// Interface representing the context of the user making requests.
/// </summary>
public interface IRequestedBy
{
    string UserId { get; }
    string CompanyId { get; }
    string CompanyName { get; }
    string ProjectId { get; }
    // Add other properties as needed based on your domain
}