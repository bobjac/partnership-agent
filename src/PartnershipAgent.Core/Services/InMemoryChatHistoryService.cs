using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public class InMemoryChatHistoryService : IChatHistoryService
{
    private readonly Dictionary<Guid, ChatHistory> chatHistories = new Dictionary<Guid, ChatHistory>();
    public async Task AddMessageToChatHistoryAsync(Guid thread_id, ChatMessageContent chatMessage)
    {
        if (!chatHistories.ContainsKey(thread_id))
        {
            chatHistories[thread_id] = new ChatHistory();
        }
        chatHistories[thread_id].Add(chatMessage);
        await Task.CompletedTask;
    }

    public async Task<ChatHistory> GetChatHistoryAsync(Guid thread_id)
    {
        if (chatHistories.TryGetValue(thread_id, out var chatHistory))
        {
            return await Task.FromResult(chatHistory);
        }
        var emptyChatHistory = new ChatHistory();
        return await Task.FromResult(emptyChatHistory);
    }
}