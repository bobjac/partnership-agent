using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Agents;

namespace PartnershipAgent.Core.Services;

public class SimpleChatProcessService
{
    private readonly IEntityResolutionAgent _entityAgent;
    private readonly IFAQAgent _faqAgent;
    private readonly ILogger<SimpleChatProcessService> _logger;

    public SimpleChatProcessService(
        IEntityResolutionAgent entityAgent,
        IFAQAgent faqAgent,
        ILogger<SimpleChatProcessService> logger)
    {
        _entityAgent = entityAgent;
        _faqAgent = faqAgent;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request)
    {
        _logger.LogInformation("Processing chat request for thread: {ThreadId}", request.ThreadId);

        try
        {
            var response = new ChatResponse
            {
                ThreadId = request.ThreadId
            };

            _logger.LogInformation("Step 1: Extracting entities from prompt");
            var entities = await _entityAgent.ExtractEntitiesAsync(request.Prompt);
            response.ExtractedEntities = entities.Select(e => e.Text).ToList();

            _logger.LogInformation("Step 2: Searching for relevant documents");
            var allowedCategories = GetAllowedCategoriesForTenant(request.TenantId);
            var documents = await _faqAgent.SearchDocumentsAsync(request.Prompt, request.TenantId, allowedCategories);
            response.RelevantDocuments = documents;

            _logger.LogInformation("Step 3: Generating response based on documents");
            var generatedResponse = await _faqAgent.GenerateResponseAsync(request.Prompt, documents);
            response.Response = generatedResponse;

            _logger.LogInformation("Chat processing completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return new ChatResponse 
            { 
                ThreadId = request.ThreadId, 
                Response = "I encountered an error processing your request. Please try again." 
            };
        }
    }

    private List<string> GetAllowedCategoriesForTenant(string tenantId)
    {
        return new List<string> { "templates", "guidelines", "policies", "contracts" };
    }
}