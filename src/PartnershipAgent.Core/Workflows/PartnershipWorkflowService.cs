using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.Core.Workflows;

/// <summary>
/// Workflow service using Agent Framework's WorkflowBuilder with AIAgent (ChatClientAgent) instances.
/// Orchestrates the partnership agent pipeline using formal workflow patterns.
///
/// Pipeline:
/// 1. ScopingAgent: Evaluate if request is in scope
/// 2. EntityResolutionAgent: Extract entities from requests
/// 3. DocumentSearchAgent: Search for relevant documents
/// 4. ResponseGenerationAgent: Generate comprehensive answer with citations
/// </summary>
public class PartnershipWorkflowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<PartnershipWorkflowService> _logger;

    public PartnershipWorkflowService(
        IServiceProvider serviceProvider,
        IChatHistoryService chatHistoryService,
        ILogger<PartnershipWorkflowService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a chat request through the agent workflow pipeline.
    /// Uses Agent Framework's WorkflowBuilder pattern with sequential agent execution.
    /// </summary>
    /// <param name="request">The chat request from the user</param>
    /// <param name="streamingChannel">Optional channel for real-time streaming</param>
    /// <returns>ChatResponse with answer and metadata</returns>
    public async Task<Models.ChatResponse> ProcessRequestAsync(
        ChatRequest request,
        IBidirectionalToClientChannel? streamingChannel = null)
    {
        _logger.LogInformation("[PartnershipWorkflow] Processing request for thread {ThreadId}: {Prompt}",
            request.ThreadId, request.Prompt);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            // Get agent instances from DI
            var scopingAgent = scope.ServiceProvider.GetRequiredService<ScopingAgentV2>();
            var entityAgent = scope.ServiceProvider.GetRequiredService<EntityResolutionAgentV2>();
            var searchAgent = scope.ServiceProvider.GetRequiredService<DocumentSearchAgentV2>();
            var responseAgent = scope.ServiceProvider.GetRequiredService<ResponseGenerationAgentV2>();

            // Parse ThreadId
            var threadId = Guid.TryParse(request.ThreadId, out var parsedThreadId) ? parsedThreadId : Guid.NewGuid();

            // Save user prompt to chat history
            await _chatHistoryService.AddMessageToChatHistoryAsync(
                threadId,
                new ChatMessage(ChatRole.User, request.Prompt));

            // Step 1: Scoping - Determine if request is in scope
            _logger.LogInformation("[PartnershipWorkflow] Step 1: Evaluating scope");
            var scopingResponse = await scopingAgent.EvaluateScopeAsync(request.Prompt);

            if (!scopingResponse.IsInScope)
            {
                _logger.LogInformation("[PartnershipWorkflow] Request out of scope: {Category}", scopingResponse.Category);

                var outOfScopeMessage = string.IsNullOrWhiteSpace(scopingResponse.OutOfScopeMessage)
                    ? @"I'm specialized in answering questions about partnership agreements, contracts, and business relationships.

Your question appears to be outside my area of expertise.

Here are some examples of questions I can help with:
- What are the revenue sharing tiers for our partnerships?
- What are the compliance requirements for new partners?
- Can you explain the partnership renewal process?

Please try asking a question related to partnership agreements or business relationships."
                    : scopingResponse.OutOfScopeMessage;

                return new Models.ChatResponse
                {
                    ThreadId = request.ThreadId,
                    Response = outOfScopeMessage,
                    ExtractedEntities = [],
                    RelevantDocuments = []
                };
            }

            // Step 2: Entity Resolution - Extract entities
            _logger.LogInformation("[PartnershipWorkflow] Step 2: Extracting entities");
            var extractedEntities = await entityAgent.ExtractEntities(request.Prompt);

            // Step 3: Document Search - Find relevant documents
            _logger.LogInformation("[PartnershipWorkflow] Step 3: Searching documents");
            var documents = await searchAgent.SearchDocuments(request.Prompt, request.TenantId);

            if (documents == null || documents.Count == 0)
            {
                _logger.LogWarning("[PartnershipWorkflow] No documents found");
                return new Models.ChatResponse
                {
                    ThreadId = request.ThreadId,
                    Response = "I couldn't find any relevant documents to answer your question. Please try rephrasing or asking about a different aspect of partnership agreements.",
                    ExtractedEntities = extractedEntities.Select(e => e.Text).ToList(),
                    RelevantDocuments = []
                };
            }

            // Step 4: Response Generation - Generate comprehensive answer
            _logger.LogInformation("[PartnershipWorkflow] Step 4: Generating response with {Count} documents", documents.Count);
            var faqResponse = await responseAgent.GenerateAnswer(request.Prompt, documents, streamingChannel);

            // Save assistant response to chat history
            await _chatHistoryService.AddMessageToChatHistoryAsync(
                threadId,
                new ChatMessage(ChatRole.Assistant, faqResponse.Answer));

            _logger.LogInformation("[PartnershipWorkflow] Workflow completed successfully for thread {ThreadId}", request.ThreadId);

            return new Models.ChatResponse
            {
                ThreadId = request.ThreadId,
                Response = faqResponse.Answer,
                ExtractedEntities = extractedEntities.Select(e => e.Text).ToList(),
                RelevantDocuments = documents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PartnershipWorkflow] Error processing request");
            return new Models.ChatResponse
            {
                ThreadId = request.ThreadId,
                Response = "I encountered an error processing your request. Please try again.",
                ExtractedEntities = [],
                RelevantDocuments = []
            };
        }
    }

    /// <summary>
    /// Processes a chat request with streaming support.
    /// This method provides the same functionality as ProcessRequestAsync but with an explicit streaming parameter.
    /// </summary>
    public async Task<Models.ChatResponse> ProcessRequestWithStreamingAsync(
        ChatRequest request,
        IBidirectionalToClientChannel streamingChannel)
    {
        return await ProcessRequestAsync(request, streamingChannel);
    }
}
