using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// In-memory implementation of chat history service using Agent Framework types.
/// </summary>
public class InMemoryChatHistoryService : IChatHistoryService
{
    private readonly Dictionary<Guid, List<ChatMessage>> _chatHistories = new();

    public Task AddMessageToChatHistoryAsync(Guid threadId, ChatMessage message)
    {
        if (!_chatHistories.ContainsKey(threadId))
        {
            _chatHistories[threadId] = new List<ChatMessage>();
        }
        _chatHistories[threadId].Add(message);
        return Task.CompletedTask;
    }

    public Task<IList<ChatMessage>> GetChatHistoryAsync(Guid threadId)
    {
        if (_chatHistories.TryGetValue(threadId, out var chatHistory))
        {
            return Task.FromResult<IList<ChatMessage>>(chatHistory);
        }
        return Task.FromResult<IList<ChatMessage>>(new List<ChatMessage>());
    }
}