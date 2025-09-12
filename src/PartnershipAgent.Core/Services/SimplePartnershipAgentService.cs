using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Simplified service that orchestrates partnership agents with controlled flow,
/// auditability, and early termination capabilities similar to your process framework.
/// </summary>
public class SimplePartnershipAgentService
{
    private static readonly ActivitySource _activitySource = new("PartnershipAgent.Core.Simple");
    private readonly IEntityResolutionAgent _entityAgent;
    private readonly IFAQAgent _faqAgent;
    private readonly ILogger _logger;

    /// <summary>
    /// Constructor for SimplePartnershipAgentService.
    /// </summary>
    public SimplePartnershipAgentService(
        IEntityResolutionAgent entityAgent,
        IFAQAgent faqAgent,
        ILoggerFactory loggerFactory
    )
    {
        _entityAgent = entityAgent ?? throw new ArgumentNullException(nameof(entityAgent));
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
        _logger = loggerFactory.CreateLogger("SimplePartnershipAgent");
    }

    /// <summary>
    /// Processes a user query with controlled orchestration and audit trail.
    /// </summary>
    /// <param name="request">The chat request containing user input and context</param>
    /// <returns>Structured chat response</returns>
    public async Task<ChatResponse> ProcessQueryAsync(ChatRequest request)
    {
        var parent = Activity.Current;
        Activity.Current = null; // Set Activity.Current to null to force StartActivity to create a new root activity

        using var activity = _activitySource.StartActivity($"SessionId: {request.ThreadId}", ActivityKind.Internal, parentId: default);
        
        var processModel = new ProcessModel
        {
            SessionId = Guid.Parse(request.ThreadId),
            Input = request.Prompt,
            InitialPrompt = request.Prompt,
            UserId = request.UserId,
            TenantId = request.TenantId
        };

        var responseChannel = new SimpleBidirectionalChannel();

        try
        {
            activity?.SetTag("request.user_prompt", request.Prompt);
            activity?.SetTag("request.user_id", request.UserId);
            activity?.SetTag("request.tenant_id", request.TenantId);

            // Step 1: Entity Resolution
            var entityResult = await ExecuteEntityResolutionStep(processModel, responseChannel);
            if (entityResult.needsClarification)
            {
                return CreateClarificationResponse(request, entityResult.message);
            }

            // Step 2: FAQ Processing
            var faqResult = await ExecuteFAQAgentStep(processModel, responseChannel);
            if (faqResult.needsClarification)
            {
                return CreateClarificationResponse(request, faqResult.message);
            }

            // Step 3: Generate Final Response
            return await GenerateFinalResponse(request, processModel, responseChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing partnership query for thread {ThreadId}", request.ThreadId);
            return new ChatResponse
            {
                ThreadId = request.ThreadId,
                Response = "I encountered an error while processing your request. Please try again.",
                ExtractedEntities = [],
                RelevantDocuments = []
            };
        }
    }

    /// <summary>
    /// Executes entity resolution step with audit logging.
    /// </summary>
    private async Task<(bool needsClarification, string message)> ExecuteEntityResolutionStep(
        ProcessModel processModel, IBidirectionalToClientChannel responseChannel)
    {
        _logger.LogInformation("Starting entity extraction for session {SessionId}", processModel.SessionId);
        
        try
        {
            await responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Analyzing your question..." }));

            var entities = await _entityAgent.ExtractEntitiesAsync(processModel.Input);
            processModel.ExtractedEntities = entities.ToList();

            _logger.LogInformation("Extracted {EntityCount} entities for session {SessionId}", 
                entities.Count(), processModel.SessionId);

            await responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Extracted {entities.Count()} entities from your query",
                    entities = entities.Select(e => e.Text).ToList()
                }));

            return (false, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities for session {SessionId}", processModel.SessionId);
            return (true, "I encountered an error while analyzing your request. Please try rephrasing your question.");
        }
    }

    /// <summary>
    /// Executes FAQ agent step with document search and response generation.
    /// </summary>
    private async Task<(bool needsClarification, string message)> ExecuteFAQAgentStep(
        ProcessModel processModel, IBidirectionalToClientChannel responseChannel)
    {
        _logger.LogInformation("Starting FAQ processing for session {SessionId}", processModel.SessionId);
        
        try
        {
            await responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Searching for relevant documents..." }));

            var allowedCategories = GetAllowedCategoriesForTenant(processModel.TenantId);
            var documents = await _faqAgent.SearchDocumentsAsync(processModel.Input, processModel.TenantId, allowedCategories);
            processModel.RelevantDocuments = documents;

            _logger.LogInformation("Found {DocumentCount} relevant documents for session {SessionId}", 
                documents.Count, processModel.SessionId);

            if (!documents.Any())
            {
                return (true, "I couldn't find any relevant documents for your question. Could you please rephrase your question or provide more specific details about partnership agreements?");
            }

            await responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Generating comprehensive answer..." }));

            var structuredResponse = await _faqAgent.GenerateStructuredResponseAsync(processModel.Input, documents);
            processModel.GeneratedResponse = structuredResponse;
            processModel.FinalResponse = structuredResponse.Answer;

            _logger.LogInformation("Generated response with confidence {Confidence} for session {SessionId}", 
                structuredResponse.ConfidenceLevel, processModel.SessionId);

            // Check if the response needs clarification based on confidence level
            if (structuredResponse.ConfidenceLevel == "low" && !structuredResponse.HasCompleteAnswer)
            {
                return (true, structuredResponse.Answer + 
                    " Could you provide more specific details about what aspect of partnership agreements you're interested in?");
            }

            return (false, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FAQ processing for session {SessionId}", processModel.SessionId);
            return (true, "I encountered an error while processing your question. Please try again.");
        }
    }

    /// <summary>
    /// Generates the final structured response.
    /// </summary>
    private async Task<ChatResponse> GenerateFinalResponse(
        ChatRequest request, ProcessModel processModel, IBidirectionalToClientChannel responseChannel)
    {
        _logger.LogInformation("Generating final response for session {SessionId}", processModel.SessionId);

        await responseChannel.WriteAsync(AIEventTypes.Completion, 
            JsonSerializer.Serialize(new { sessionId = processModel.SessionId, timestamp = DateTime.UtcNow }));

        return new ChatResponse
        {
            ThreadId = request.ThreadId,
            Response = processModel.GeneratedResponse?.Answer ?? "I was unable to generate a response.",
            ExtractedEntities = processModel.ExtractedEntities.Select(e => e.Text).ToList(),
            RelevantDocuments = processModel.RelevantDocuments
        };
    }

    /// <summary>
    /// Creates a clarification response when user input needs clarification.
    /// </summary>
    private static ChatResponse CreateClarificationResponse(ChatRequest request, string message)
    {
        return new ChatResponse
        {
            ThreadId = request.ThreadId,
            Response = message,
            ExtractedEntities = [],
            RelevantDocuments = []
        };
    }

    /// <summary>
    /// Gets the allowed document categories for a given tenant.
    /// </summary>
    private static List<string> GetAllowedCategoriesForTenant(string tenantId)
    {
        // In a real implementation, this would be based on tenant permissions
        return ["templates", "guidelines", "policies", "contracts"];
    }
}