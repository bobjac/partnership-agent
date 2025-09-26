using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Evaluation;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles entity extraction from user input using the EntityResolutionAgent.
/// </summary>
public class EntityResolutionStep : KernelProcessStep
{
    private readonly EntityResolutionAgent _entityResolutionAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<EntityResolutionStep> _logger;
    private readonly IAssistantResponseEvaluator? _evaluator;

    /// <summary>
    /// Constructor for EntityResolutionStep.
    /// </summary>
    /// <param name="entityResolutionAgent">Agent for extracting entities from user input</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public EntityResolutionStep(
        EntityResolutionAgent entityResolutionAgent,
        IBidirectionalToClientChannel responseChannel,
        IChatHistoryService chatHistoryService,
        ILogger<EntityResolutionStep> logger,
        IAssistantResponseEvaluator? evaluator = null)
    {
        _entityResolutionAgent = entityResolutionAgent ?? throw new ArgumentNullException(nameof(entityResolutionAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _evaluator = evaluator;
    }

    /// <summary>
    /// Executes entity extraction and emits appropriate events based on the result.
    /// </summary>
    /// <param name="context">The KernelProcessStepContext that exposes framework services</param>
    /// <param name="kernel">SemanticKernel Kernel object</param>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Task representing the asynchronous operation</returns>
    [KernelFunction]
    [Description("Extracts entities from user input and prepares for document search")]
    public async Task ExtractEntitiesAsync(KernelProcessStepContext context, Kernel kernel, ProcessModel processModel)
    {
        try
        {
            _logger.LogInformation("Starting entity extraction for session {ThreadId}", processModel.ThreadId);
            
            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Analyzing your question..." }));

            // Use the EntityResolutionAgent with LLM-driven approach
            var agentMessage = $"""
                User Input: {processModel.Input}

                Please analyze this text and extract relevant entities focusing on partnership and business terminology.
                """;

            await _chatHistoryService.AddMessageToChatHistoryAsync(processModel.ThreadId, new ChatMessageContent(AuthorRole.User, agentMessage));
            var chatMessages = await _chatHistoryService.GetChatHistoryAsync(processModel.ThreadId);
            var responseList = await _entityResolutionAgent.InvokeAsync(chatMessages).ToListAsync();
            var assistantMessage = responseList.LastOrDefault(m => m.Role == AuthorRole.Assistant);
            var lastMessage = assistantMessage != null ? assistantMessage.Content : string.Empty;
            if (assistantMessage == null)
            {
                _logger.LogWarning("No assistant message found in responseList for session {ThreadId}", processModel.ThreadId);
            }
            await _chatHistoryService.AddMessageToChatHistoryAsync(processModel.ThreadId, new ChatMessageContent(AuthorRole.Assistant, lastMessage));

            // Evaluate the entity extraction response if evaluator is available
            if (_evaluator != null && !string.IsNullOrWhiteSpace(lastMessage))
            {
                try
                {
                    using var activity = System.Diagnostics.Activity.Current?.Source?.StartActivity("EntityResolution Evaluation");
                    _ = await _evaluator.EvaluateAndLogAsync(
                        userPrompt: processModel.Input,
                        response: lastMessage,
                        module: "EntityResolutionAgent",
                        parentActivity: activity?.Source,
                        expectedAnswer: null
                    );
                }
                catch (Exception evalEx)
                {
                    _logger.LogWarning(evalEx, "Failed to evaluate entity resolution response for session {ThreadId}", processModel.ThreadId);
                    // Continue without failing the request
                }
            }

            EntityResolutionResponse entityResolutionResponse = null;
            if (!string.IsNullOrWhiteSpace(lastMessage))
            {
                try
                {
                    entityResolutionResponse = JsonSerializer.Deserialize<EntityResolutionResponse>(
                        lastMessage,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize entity resolution response for session {ThreadId}. Content: {Content}", processModel.ThreadId, lastMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error during deserialization of entity resolution response for session {ThreadId}. Content: {Content}", processModel.ThreadId, lastMessage);
                }
            }

            // Extract entities from the structured response
            var entities = new List<ExtractedEntity>();
            
            if (entityResolutionResponse?.ExtractedEntities?.Any() == true)
            {
                // Use entities from the structured response
                entities = entityResolutionResponse.ExtractedEntities;
            }
            else
            {
                // Fallback: Use a default entity to ensure processing continues
                entities = new List<ExtractedEntity>
                {
                    new ExtractedEntity 
                    { 
                        Text = "general inquiry", 
                        Type = "general", 
                        Confidence = 0.6 
                    }
                };
            }

            processModel.ExtractedEntities = entities;

            _logger.LogInformation("Extracted {EntityCount} entities for session {ThreadId}", 
                entities.Count, processModel.ThreadId);

            // Send status update with extracted entities
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Extracted {entities.Count} entities from your query",
                    entities = entities.Select(e => e.Text).ToList()
                }));

            // Always proceed to document search - we can handle queries even without explicit entities
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.EntityExtractionCompleted,
                Data = processModel
            });
        }
        catch (Exception ex) when (LogError(ex, $"Error extracting entities for session {processModel.ThreadId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while analyzing your request. Please try rephrasing your question.";
            
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ProcessError,
                Data = processModel
            });
        }
    }

    /// <summary>
    /// Logs an error with a consistent message template.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">The error message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogError(Exception ex, string message)
    {
        _logger.LogError(ex, "Entity resolution step error: {ErrorMessage}", message);
        return true;
    }
}