using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Base class for kernel process steps that provides common functionality and logging.
/// </summary>
public abstract class BaseKernelProcessStep
{
    /// <summary>
    /// Logger instance for the step.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Response channel for sending responses to the client.
    /// </summary>
    protected readonly IBidirectionalToClientChannel ResponseChannel;

    /// <summary>
    /// Initializes a new instance of the BaseKernelProcessStep class.
    /// </summary>
    /// <param name="responseChannel">The response channel for client communication</param>
    /// <param name="logger">Logger instance for the step</param>
    protected BaseKernelProcessStep(IBidirectionalToClientChannel responseChannel, ILogger logger)
    {
        ResponseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
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
/// Interface for bidirectional communication channel to the client.
/// For now, this is a simple implementation that collects responses.
/// </summary>
public interface IBidirectionalToClientChannel
{
    /// <summary>
    /// Writes a message to the client.
    /// </summary>
    /// <param name="eventType">The type of event being sent</param>
    /// <param name="content">The content to send</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task WriteAsync(string eventType, string content);

    /// <summary>
    /// Gets the full response that has been written to this channel.
    /// </summary>
    /// <returns>The complete response content</returns>
    string GetFullResponse();
}

/// <summary>
/// Simple implementation of IBidirectionalToClientChannel for basic scenarios.
/// </summary>
public class SimpleBidirectionalChannel : IBidirectionalToClientChannel
{
    private readonly List<string> _responses = new();

    public async Task WriteAsync(string eventType, string content)
    {
        _responses.Add($"[{eventType}] {content}");
        await Task.CompletedTask;
    }

    public string GetFullResponse()
    {
        return string.Join(Environment.NewLine, _responses);
    }
}