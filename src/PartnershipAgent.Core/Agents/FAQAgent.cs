using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// The FAQAgent is a specialized agent that answers questions about partnership agreements
/// using semantic search and structured responses.
/// </summary>
public class FAQAgent : BaseChatHistoryAgent, IFAQAgent
{
    private readonly IKernelBuilder _kernelBuilder;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly ICitationService _citationService;

    /// <summary>
    /// The name of the agent used for identification in the semantic kernel system.
    /// </summary>
    public override string Name => "FAQAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that answers questions about partnership agreements using document search and structured responses";

    /// <summary>
    /// Constructor for the FAQAgent.
    /// Initializes the agent with required dependencies and configures the semantic kernel.
    /// </summary>
    public FAQAgent(
        Guid threadId,
        IKernelBuilder kernelBuilder,
        IElasticSearchService elasticSearchService,
        ICitationService citationService,
        IRequestedBy requestedBy,
        ILogger<FAQAgent> logger
    ) : base(requestedBy, threadId, logger)
    {
        _kernelBuilder = kernelBuilder ?? throw new ArgumentNullException(nameof(kernelBuilder));
        _elasticSearchService = elasticSearchService ?? throw new ArgumentNullException(nameof(elasticSearchService));
        _citationService = citationService ?? throw new ArgumentNullException(nameof(citationService));

        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatCompletionAgent with the appropriate settings and instructions.
    /// </summary>
    private void InitializeAgent()
    {
        Kernel kernel = _kernelBuilder.Build();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            ResponseFormat = typeof(FAQAgentResponse)
        };

        var arguments = new KernelArguments(executionSettings);
        var instructions = @"
            You are a helpful assistant that answers questions about partnership agreements.
            Use the SearchDocuments function to find relevant documents, then provide structured responses.
            
            Always respond with:
            - A comprehensive answer based on the available information
            - Your confidence level (high/medium/low) based on document relevance and completeness
            - List of source document titles that were used
            - Whether you have enough information for a complete answer
            - 2-3 relevant follow-up questions the user might ask
            
            If the answer is not in the provided documents, be honest about it in your response.
        ";

        Agent = new ChatCompletionAgent
        {
            Name = Name,
            Description = Description,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = arguments
        };

        Agent.Kernel.Plugins.AddFromObject(this);
    }

    /// <summary>\n    /// Simple implementation of IRequestedBy for this context.\n    /// In a real implementation, this would come from your authentication/authorization system.\n    /// </summary>\n    private class SimpleRequestedBy : IRequestedBy\n    {\n        public string UserId { get; set; } = \"mock-user-123\";\n        public string CompanyId { get; set; } = \"company-123\";\n        public string CompanyName { get; set; } = \"Default Company\";\n        public string ProjectId { get; set; } = \"project-123\";\n    }

    /// <summary>
    /// Searches for documents relevant to the query using Elasticsearch.
    /// This method is exposed as a KernelFunction for the agent to use.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="tenantId">The tenant identifier for multi-tenancy</param>
    /// <param name="allowedCategories">Optional list of allowed document categories</param>
    /// <returns>List of relevant documents</returns>
    [KernelFunction, Description("Searches for documents relevant to the user's question about partnership agreements.")]
    public async Task<List<DocumentResult>> SearchDocuments(
        [Description("The search query based on the user's question")] string query,
        [Description("The tenant identifier")] string tenantId = "tenant-123",
        [Description("Optional list of allowed document categories")] List<string>? allowedCategories = null)
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

    /// <summary>
    /// Generates a comprehensive answer based on found documents.
    /// This method is exposed as a KernelFunction for the agent to use.
    /// </summary>
    /// <param name="query">The user's question</param>
    /// <param name="documents">The relevant documents found by search</param>
    /// <returns>Structured response with answer, confidence, and metadata</returns>
    [KernelFunction, Description("Generates a comprehensive answer to the user's question based on relevant documents.")]
    public async Task<FAQAgentResponse> GenerateAnswer(
        [Description("The user's original question")] string query,
        [Description("List of relevant documents to base the answer on")] List<DocumentResult> documents)
    {
        Logger.LogInformation("Generating structured response based on {Count} documents for conversation thread {ThreadId}", documents.Count, ThreadId);

        try
        {
            var context = string.Join("\n\n", documents.Select(d => 
                $"Document: {d.Title}\nCategory: {d.Category}\nContent: {d.Content}"));

            // In the agent-based approach, the LLM will automatically structure the response
            // based on the ResponseFormat specified in the agent configuration
            var hasRelevantInfo = documents.Any() && !string.IsNullOrEmpty(context);
            var confidence = documents.Count >= 2 ? "high" : documents.Count == 1 ? "medium" : "low";
            
            // Generate the answer content
            var answerContent = hasRelevantInfo ? 
                $"Based on the {documents.Count} relevant document(s), here is the answer to your question about {query}." : 
                "I don't have enough information in the available documents to answer your question.";

            // Extract citations for the answer
            var citations = hasRelevantInfo ? 
                await _citationService.ExtractCitationsAsync(query, answerContent, documents) : 
                new List<DocumentCitation>();
            
            // This is a simplified version - in practice, the LLM would generate this via the agent
            var response = new FAQAgentResponse
            {
                Answer = answerContent,
                ConfidenceLevel = confidence,
                HasCompleteAnswer = hasRelevantInfo,
                SourceDocuments = documents.Select(d => d.Title).ToList(),
                Citations = citations,
                FollowUpSuggestions = hasRelevantInfo ? [
                    "What are the specific requirements for partnership compliance?",
                    "How are revenue calculations performed?",
                    "What are the termination procedures for partnerships?"
                ] : []
            };

            Logger.LogInformation("Generated structured response with confidence: {Confidence} and {CitationCount} citations", 
                response.ConfidenceLevel, citations.Count);
            return response;
        }
        catch (Exception ex) when (Log(ex, $"Error generating answer for session {ThreadId} with query {query}"))
        {
            return new FAQAgentResponse
            {
                Answer = "I encountered an error while processing your request. Please try again.",
                ConfidenceLevel = "low",
                HasCompleteAnswer = false
            };
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public async Task<FAQAgentResponse> GenerateStructuredResponseAsync(string query, List<DocumentResult> relevantDocuments)
    {
        // For now, delegate to the async version
        // In a full implementation, this would use the Agent's chat completion capabilities
        return await GenerateAnswer(query, relevantDocuments);
    }

    /// <summary>
    /// Legacy method for backward compatibility - returns just the answer text.
    /// </summary>
    public async Task<string> GenerateResponseAsync(string query, List<DocumentResult> relevantDocuments)
    {
        var structuredResponse = await GenerateStructuredResponseAsync(query, relevantDocuments);
        return structuredResponse.Answer;
    }
}