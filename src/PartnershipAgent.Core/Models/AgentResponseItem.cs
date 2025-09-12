using System;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PartnershipAgent.Core.Models;

/// <summary>
/// Wrapper for agent responses that includes metadata about the response.
/// </summary>
/// <typeparam name="T">The type of the message content</typeparam>
public class AgentResponseItem<T>
{
    /// <summary>
    /// The message content from the agent.
    /// </summary>
    public T Message { get; set; }

    /// <summary>
    /// The agent that generated this response.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the response was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Constructor for AgentResponseItem.
    /// </summary>
    /// <param name="message">The message content</param>
    /// <param name="agentName">Name of the agent that generated the response</param>
    public AgentResponseItem(T message, string agentName = "")
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        AgentName = agentName;
    }
}

/// <summary>
/// Simple text agent response for basic communication.
/// </summary>
public class TextAgentResponse
{
    /// <summary>
    /// The text content of the response.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Constructor for TextAgentResponse.
    /// </summary>
    /// <param name="content">The text content</param>
    public TextAgentResponse(string content)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }
}