using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartnershipAgent.Core.Services;

public interface IChatHistoryService
{
    Task AddMessageToChatHistoryAsync(Guid thread_id, ChatMessageContent chatMessage);
    Task<ChatHistory> GetChatHistoryAsync(Guid thread_id);
}