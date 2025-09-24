using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public class LocalDeviceChatHistoryService : IChatHistoryService
{
    private ChatHistory chatMessages = new ChatHistory(new List<ChatMessageContent>() { new ChatMessageContent(AuthorRole.User, "FirstMessageHelloWorld") });
    public async Task AddChatMessageAsync(Guid thread_id, string chatMessage)
    {
        chatMessages.Add(new ChatMessageContent(AuthorRole.User, chatMessage));
    }

    public async Task<ChatHistory> GetChatHistoryAsync(Guid thread_id)
    {
        return chatMessages;
    }
}

