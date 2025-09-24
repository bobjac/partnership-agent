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
    private ChatHistory chatMessages = new ChatHistory();
    public async Task AddMessageToChatHistoryAsync(Guid thread_id, ChatMessageContent chatMessage)
    {
        chatMessages.Add(chatMessage);
    }

    public async Task<ChatHistory> GetChatHistoryAsync(Guid thread_id)
    {
        return chatMessages;
    }
}