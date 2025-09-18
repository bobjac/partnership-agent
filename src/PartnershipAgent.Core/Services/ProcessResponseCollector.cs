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
    /// <param name="sessionId">The session ID</param>
    /// <param name="response">The final chat response</param>
    public void SetResponse(Guid sessionId, ChatResponse response)
    {
        _responses.TryAdd(sessionId, response);
    }

    /// <summary>
    /// Gets and removes the final response for a session
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The final chat response, or null if not found</returns>
    public ChatResponse? GetAndRemoveResponse(Guid sessionId)
    {
        _responses.TryRemove(sessionId, out var response);
        return response;
    }
}