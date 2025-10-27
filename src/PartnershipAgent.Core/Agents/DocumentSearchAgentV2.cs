using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Agent Framework (v2) implementation for document search functionality.
/// Specialized agent that searches for partnership agreement documents using semantic search.
/// Split from original FAQAgent to separate concerns.
/// </summary>
public class DocumentSearchAgentV2 : BaseAgent
{
    private readonly IChatClient _chatClient;
    private readonly IElasticSearchService _elasticSearchService;

    /// <summary>
    /// The name of the agent used for identification in the Agent Framework system.
    /// </summary>
    public override string Name => "DocumentSearchAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that searches for relevant partnership agreement documents";

    /// <summary>
    /// Constructor for the DocumentSearchAgentV2.
    /// Initializes the agent with required dependencies.
    /// </summary>
    public DocumentSearchAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IElasticSearchService elasticSearchService,
        IRequestedBy requestedBy,
        ILogger<DocumentSearchAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _elasticSearchService = elasticSearchService ?? throw new ArgumentNullException(nameof(elasticSearchService));
        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatClientAgent with the appropriate settings and instructions.
    /// </summary>
    private void InitializeAgent()
    {
        var instructions = @"
            You are a document search assistant for partnership agreements.
            Your role is to help find relevant documents based on user queries.

            Focus on understanding the user's intent and formulating effective search queries
            for partnership agreements, contracts, and related business documents.
        ";

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });
    }

    /// <summary>
    /// Searches for documents relevant to the query using Elasticsearch.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="tenantId">The tenant identifier for multi-tenancy</param>
    /// <param name="allowedCategories">Optional list of allowed document categories</param>
    /// <returns>List of relevant documents</returns>
    public async Task<List<DocumentResult>> SearchDocuments(
        string query,
        string tenantId = "tenant-123",
        List<string>? allowedCategories = null)
    {
        Logger.LogInformation("Searching documents for tenant {TenantId} with query: {Query}", tenantId, query);

        try
        {
            var documents = await _elasticSearchService.SearchDocumentsAsync(query, tenantId, allowedCategories ?? []);
            Logger.LogInformation("Found {Count} relevant documents", documents.Count);
            return documents;
        }
        catch (Exception ex) when (Log(ex, $"Error searching documents for session {ThreadId} with query {query}"))
        {
            return [];
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public async Task<List<DocumentResult>> SearchDocumentsAsync(string query, string tenantId, List<string> allowedCategories)
    {
        return await SearchDocuments(query, tenantId, allowedCategories);
    }
}
