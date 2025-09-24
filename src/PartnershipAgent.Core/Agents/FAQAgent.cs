using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// The FAQAgent is a specialized agent that answers questions about partnership agreements
/// using semantic search and structured responses.
/// </summary>
public class FAQAgent : BaseChatHistoryAgent
{
    private readonly IKernelBuilder _kernelBuilder;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly ICitationService _citationService;
    private readonly IChatHistoryService _chatHistoryService;

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
        IChatHistoryService chatHistoryService,
        IRequestedBy requestedBy,
        ILogger<FAQAgent> logger
    ) : base(requestedBy, threadId, logger)
    {
        _kernelBuilder = kernelBuilder ?? throw new ArgumentNullException(nameof(kernelBuilder));
        _elasticSearchService = elasticSearchService ?? throw new ArgumentNullException(nameof(elasticSearchService));
        _citationService = citationService ?? throw new ArgumentNullException(nameof(citationService));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));

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
            - Detailed citations for each document used, including document ID, title, category, relevant excerpts, and relevance scores
            - Whether you have enough information for a complete answer
            - 2-3 relevant follow-up questions the user might ask
            
            For Citations, create detailed DocumentCitation objects for each document you reference, including:
            - DocumentId: Use the document's ID
            - DocumentTitle: The document's title
            - Category: The document's category  
            - Excerpt: The specific text from the document that supports your answer
            - RelevanceScore: A score from 0.0 to 1.0 indicating how relevant this document is
            
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

            var hasRelevantInfo = documents.Any() && !string.IsNullOrEmpty(context);
            var confidence = documents.Count >= 2 ? "high" : documents.Count == 1 ? "medium" : "low";
            
            FAQAgentResponse response;
            
            if (hasRelevantInfo)
            {
                // Use the AI agent to generate a comprehensive answer based on the documents
                var prompt = $"Question: {query}\n\nRelevant Documents:\n{context}\n\nPlease provide a comprehensive answer based on the provided documents.";

                // Invoke the AI agent to generate the structured response
                await _chatHistoryService.AddChatMessageAsync(ThreadId, prompt);
                var chatHistory = await _chatHistoryService.GetChatHistoryAsync(ThreadId);
                
                var agentResponses = InvokeAsync(chatHistory);
                var lastResponse = "";
                
                await foreach (var agentResponse in agentResponses)
                {
                    if (agentResponse is ChatMessageContent messageContent)
                    {
                        lastResponse = messageContent.Content ?? "";
                    }
                }
                
                response = new FAQAgentResponse
                {
                    Answer = !string.IsNullOrEmpty(lastResponse) ? lastResponse : "I was unable to generate a response based on the provided documents.",
                    ConfidenceLevel = confidence,
                    HasCompleteAnswer = hasRelevantInfo,
                    SourceDocuments = documents.Select(d => d.Title).ToList(),
                    Citations = new List<DocumentCitation>(),
                    FollowUpSuggestions = hasRelevantInfo ? [
                        "What are the specific requirements for partnership compliance?",
                        "How are revenue calculations performed?",
                        "What are the termination procedures for partnerships?"
                    ] : []
                };
            }
            else
            {
                response = new FAQAgentResponse
                {
                    Answer = "I don't have enough information in the available documents to answer your question.",
                    ConfidenceLevel = "low",
                    HasCompleteAnswer = false,
                    SourceDocuments = new List<string>(),
                    Citations = new List<DocumentCitation>(),
                    FollowUpSuggestions = new List<string>()
                };
            }

            // Extract citations for the answer if we have content
            if (hasRelevantInfo && !string.IsNullOrEmpty(response.Answer))
            {
                response.Citations = await _citationService.ExtractCitationsAsync(query, response.Answer, documents);
            }

            Logger.LogInformation("Generated structured response with confidence: {Confidence} and {CitationCount} citations", 
                response.ConfidenceLevel, response.Citations?.Count ?? 0);
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