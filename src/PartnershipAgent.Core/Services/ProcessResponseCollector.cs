using System;
using System.Collections.Concurrent;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Service to collect final responses from Semantic Kernel process execution
/// </summary>
public class ProcessResponseCollector
{
    private readonly ConcurrentDictionary<Guid, ChatResponse> _responses = new();

    /// <summary>
    /// Stores a final response for a session
    /// </summary>
    /// <param name="threadId">The session ID</param>
    /// <param name="response">The final chat response</param>
    public void SetResponse(Guid threadId, ChatResponse response)
    {
        _responses.TryAdd(threadId, response);
    }

    /// <summary>
    /// Gets and removes the final response for a session
    /// </summary>
    /// <param name="threadId">The session ID</param>
    /// <returns>The final chat response, or null if not found</returns>
    public ChatResponse? GetAndRemoveResponse(Guid threadId)
    {
        _responses.TryRemove(threadId, out var response);
        return response;
    }
}