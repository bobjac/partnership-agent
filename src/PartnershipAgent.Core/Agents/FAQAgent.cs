using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

public class FAQAgent : IFAQAgent
{
    private readonly Kernel _kernel;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly ILogger<FAQAgent> _logger;

    public FAQAgent(Kernel kernel, IElasticSearchService elasticSearchService, ILogger<FAQAgent> logger)
    {
        _kernel = kernel;
        _elasticSearchService = elasticSearchService;
        _logger = logger;
    }

    public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories)
    {
        _logger.LogInformation("Searching documents for tenant {TenantId} with query: {Query}", tenantId, query);
        
        try
        {
            var documents = await _elasticSearchService.SearchDocumentsAsync(query, tenantId, allowedCategories);
            _logger.LogInformation("Found {Count} relevant documents", documents.Count);
            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return new List<DocumentResult>();
        }
    }

    public async Task<string> GenerateResponseAsync(string query, List<DocumentResult> relevantDocuments)
    {
        _logger.LogInformation("Generating response based on {Count} documents", relevantDocuments.Count);

        var context = string.Join("\n\n", relevantDocuments.Select(d => 
            $"Document: {d.Title}\nCategory: {d.Category}\nContent: {d.Content}"));

        var function = _kernel.CreateFunctionFromPrompt(@"
            You are a helpful assistant that answers questions about partnership agreements.
            Use the following context documents to answer the user's question.
            If the answer is not in the provided documents, say so clearly.
            
            Context:
            {{$context}}
            
            User Question: {{$query}}
            
            Please provide a comprehensive answer based on the available information.
        ");

        try
        {
            var result = await _kernel.InvokeAsync(function, new() 
            { 
                ["context"] = context,
                ["query"] = query 
            });

            var response = result.ToString();
            _logger.LogInformation("Generated response of length {Length}", response.Length);
            
            return string.IsNullOrEmpty(response) 
                ? "I don't have enough information in the available documents to answer your question." 
                : response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response");
            return "I encountered an error while processing your request. Please try again.";
        }
    }
}