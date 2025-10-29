using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Service for managing chat history using Agent Framework types.
/// </summary>
public interface IChatHistoryService
{
    /// <summary>
    /// Adds a message to the chat history for a specific thread.
    /// </summary>
    Task AddMessageToChatHistoryAsync(Guid threadId, ChatMessage message);

    /// <summary>
    /// Retrieves the complete chat history for a specific thread.
    /// </summary>
    Task<IList<ChatMessage>> GetChatHistoryAsync(Guid threadId);
}