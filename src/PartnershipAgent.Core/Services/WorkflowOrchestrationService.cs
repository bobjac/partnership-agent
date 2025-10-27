using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Evaluation;
using PartnershipAgent.Core.Steps;
using PartnershipAgent.Core.Models;
using ChatResponse = PartnershipAgent.Core.Models.ChatResponse;
using ChatRequest = PartnershipAgent.Core.Models.ChatRequest;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Agent Framework (v2) orchestration service that coordinates V2 agents
/// in a workflow pattern. Replaces Semantic Kernel's Process Framework with
/// direct agent invocation and conditional routing.
/// </summary>
public class WorkflowOrchestrationService
{
    private static readonly ActivitySource _activitySource = new("PartnershipAgent.WorkflowOrchestration");
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowOrchestrationService> _logger;
    private readonly IAssistantResponseEvaluator? _evaluator;

    /// <summary>
    /// Constructor for WorkflowOrchestrationService.
    /// </summary>
    public WorkflowOrchestrationService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowOrchestrationService> logger,
        IAssistantResponseEvaluator? evaluator = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _evaluator = evaluator;
    }

    /// <summary>
    /// Processes a chat request through the V2 agent workflow.
    /// </summary>
    /// <param name="request">The chat request to process</param>
    /// <returns>The final chat response</returns>
    public async Task<ChatResponse> ProcessRequestAsync(ChatRequest request)
    {
        return await ProcessRequestAsync(request, null);
    }

    /// <summary>
    /// Processes a chat request through the V2 agent workflow with streaming support.
    /// </summary>
    /// <param name="request">The chat request to process</param>
    /// <param name="streamingChannel">Optional streaming channel for real-time communication</param>
    /// <returns>The final chat response</returns>
    public async Task<ChatResponse> ProcessRequestAsync(ChatRequest request, IBidirectionalToClientChannel? streamingChannel)
    {
        var parent = Activity.Current;
        Activity.Current = null;

        using var activity = _activitySource.StartActivity($"ThreadId: {request.ThreadId}", ActivityKind.Internal, parentId: default);

        var threadId = Guid.TryParse(request.ThreadId, out var threadGuid) ? threadGuid : Guid.NewGuid();

        try
        {
            activity?.SetTag("request.user_prompt", request.Prompt);
            activity?.SetTag("request.user_id", request.UserId);
            activity?.SetTag("request.tenant_id", request.TenantId);

            _logger.LogInformation("Starting workflow orchestration for session {ThreadId}", threadId);

            // Save the user's initial prompt to chat history
            var chatHistoryService = _serviceProvider.GetRequiredService<IChatHistoryService>();
            await chatHistoryService.AddMessageToChatHistoryAsync(threadId, new ChatMessageContent(AuthorRole.User, request.Prompt));

            // ============================================================================
            // Step 1: Scoping - Determine if request is in scope
            // ============================================================================

            var scopingAgent = _serviceProvider.GetRequiredService<ScopingAgentV2>();
            _logger.LogInformation("[Workflow] Step 1: Running ScopingAgent");

            var scopingResponse = await scopingAgent.EvaluateScopeAsync(request.Prompt);

            if (!scopingResponse.IsInScope)
            {
                _logger.LogInformation("[Workflow] Request is out of scope, returning early");

                var outOfScopeMessage = scopingResponse.OutOfScopeMessage
                    ?? "I'm sorry, but that question is outside my area of expertise. I can help with partnership agreements, contracts, and related business matters.";

                if (scopingResponse.Suggestions.Any())
                {
                    outOfScopeMessage += "\n\nHere are some topics I can help with:\n" +
                        string.Join("\n", scopingResponse.Suggestions.Select(s => $"- {s}"));
                }

                return new ChatResponse
                {
                    ThreadId = request.ThreadId,
                    Response = outOfScopeMessage,
                    ExtractedEntities = [],
                    RelevantDocuments = []
                };
            }

            _logger.LogInformation("[Workflow] Request is in scope, proceeding to entity extraction");

            // ============================================================================
            // Step 2: Entity Resolution - Extract entities from the prompt
            // ============================================================================

            var entityAgent = _serviceProvider.GetRequiredService<EntityResolutionAgentV2>();
            _logger.LogInformation("[Workflow] Step 2: Running EntityResolutionAgent");

            var extractedEntities = await entityAgent.ExtractEntities(request.Prompt);

            _logger.LogInformation("[Workflow] Extracted {Count} entities", extractedEntities.Count);

            // ============================================================================
            // Step 3: Document Search - Find relevant documents
            // ============================================================================

            var searchAgent = _serviceProvider.GetRequiredService<DocumentSearchAgentV2>();
            _logger.LogInformation("[Workflow] Step 3: Running DocumentSearchAgent");

            var documents = await searchAgent.SearchDocuments(request.Prompt, request.TenantId ?? "tenant-123");

            if (documents == null || documents.Count == 0)
            {
                _logger.LogInformation("[Workflow] No documents found, returning clarification message");

                return new ChatResponse
                {
                    ThreadId = request.ThreadId,
                    Response = "I couldn't find any relevant documents to answer your question. Could you please rephrase or provide more details?",
                    ExtractedEntities = extractedEntities.Select(e => e.Text).ToList(),
                    RelevantDocuments = []
                };
            }

            _logger.LogInformation("[Workflow] Found {Count} relevant documents", documents.Count);

            // ============================================================================
            // Step 4: Response Generation - Generate comprehensive answer
            // ============================================================================

            var responseAgent = _serviceProvider.GetRequiredService<ResponseGenerationAgentV2>();
            _logger.LogInformation("[Workflow] Step 4: Running ResponseGenerationAgent");

            if (streamingChannel != null)
            {
                _logger.LogInformation("[Workflow] Streaming enabled for response generation");
            }

            var faqResponse = await responseAgent.GenerateAnswer(request.Prompt, documents, streamingChannel);

            _logger.LogInformation("[Workflow] Response generated with confidence: {Confidence}", faqResponse.ConfidenceLevel);

            var chatResponse = new ChatResponse
            {
                ThreadId = request.ThreadId,
                Response = faqResponse.Answer,
                ExtractedEntities = extractedEntities.Select(e => e.Text).ToList(),
                RelevantDocuments = documents
            };

            // Save assistant response to chat history
            await chatHistoryService.AddMessageToChatHistoryAsync(threadId, new ChatMessageContent(AuthorRole.Assistant, faqResponse.Answer));

            // Evaluate the response if evaluator is available (fire-and-forget)
            if (_evaluator != null && !string.IsNullOrWhiteSpace(chatResponse.Response))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = await _evaluator.EvaluateAndLogAsync(
                            userPrompt: request.Prompt,
                            response: chatResponse.Response,
                            module: "PartnershipAgent.WorkflowV2",
                            parentActivity: _activitySource,
                            expectedAnswer: null
                        );
                    }
                    catch (Exception evalEx)
                    {
                        _logger.LogWarning(evalEx, "Failed to evaluate response for session {ThreadId}", threadId);
                    }
                });
            }

            _logger.LogInformation("Workflow orchestration completed for session {ThreadId}", threadId);

            return chatResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in workflow orchestration for session {ThreadId}", threadId);

            return new ChatResponse
            {
                ThreadId = request.ThreadId,
                Response = "I encountered an error while processing your request. Please try again.",
                ExtractedEntities = [],
                RelevantDocuments = []
            };
        }
    }
}
