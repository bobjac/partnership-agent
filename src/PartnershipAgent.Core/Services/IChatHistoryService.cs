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
    Task AddChatMessageAsync(Guid thread_id, string chatMessage);
    Task<ChatHistory> GetChatHistoryAsync(Guid thread_id);
}