using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Steps;

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Service that orchestrates the execution of partnership agent steps using event-driven transitions.
/// This provides controlled flow, auditability, and early termination capabilities.
/// </summary>
public class StepOrchestrationService
{
    private static readonly ActivitySource _activitySource = new("PartnershipAgent.StepOrchestration");
    private readonly EntityResolutionStep _entityResolutionStep;
    private readonly DocumentSearchStep _documentSearchStep;
    private readonly ResponseGenerationStep _responseGenerationStep;
    private readonly UserResponseStep _userResponseStep;
    private readonly ILogger<StepOrchestrationService> _logger;

    /// <summary>
    /// Constructor for StepOrchestrationService.
    /// </summary>
    public StepOrchestrationService(
        EntityResolutionStep entityResolutionStep,
        DocumentSearchStep documentSearchStep,
        ResponseGenerationStep responseGenerationStep,
        UserResponseStep userResponseStep,
        ILogger<StepOrchestrationService> logger)
    {
        _entityResolutionStep = entityResolutionStep ?? throw new ArgumentNullException(nameof(entityResolutionStep));
        _documentSearchStep = documentSearchStep ?? throw new ArgumentNullException(nameof(documentSearchStep));
        _responseGenerationStep = responseGenerationStep ?? throw new ArgumentNullException(nameof(responseGenerationStep));
        _userResponseStep = userResponseStep ?? throw new ArgumentNullException(nameof(userResponseStep));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a chat request through the orchestrated step pipeline.
    /// </summary>
    /// <param name="request">The chat request to process</param>
    /// <returns>The final chat response</returns>
    public async Task<ChatResponse> ProcessRequestAsync(ChatRequest request)
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

        try
        {
            activity?.SetTag("request.user_prompt", request.Prompt);
            activity?.SetTag("request.user_id", request.UserId);
            activity?.SetTag("request.tenant_id", request.TenantId);

            _logger.LogInformation("Starting step orchestration for session {SessionId}", processModel.SessionId);

            // Execute the step pipeline with event-driven transitions
            var currentEvent = AgentOrchestrationEvents.StartProcess;
            var stepExecutions = new List<string>();

            while (currentEvent != AgentOrchestrationEvents.ProcessCompleted && currentEvent != AgentOrchestrationEvents.ProcessError)
            {
                var nextEvent = await ExecuteStepByEvent(currentEvent, processModel);
                stepExecutions.Add($"{currentEvent} -> {nextEvent}");
                
                _logger.LogInformation("Step transition: {CurrentEvent} -> {NextEvent} for session {SessionId}", 
                    currentEvent, nextEvent, processModel.SessionId);
                
                currentEvent = nextEvent;

                // Safety check to prevent infinite loops
                if (stepExecutions.Count > 10)
                {
                    _logger.LogError("Too many step executions for session {SessionId}. Possible infinite loop detected.", processModel.SessionId);
                    break;
                }
            }

            _logger.LogInformation("Step orchestration completed for session {SessionId}. Final event: {FinalEvent}", 
                processModel.SessionId, currentEvent);

            activity?.SetTag("orchestration.final_event", currentEvent);
            activity?.SetTag("orchestration.steps_executed", stepExecutions.Count);

            return CreateChatResponse(request, processModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in step orchestration for session {SessionId}", processModel.SessionId);
            return CreateErrorResponse(request);
        }
    }

    /// <summary>
    /// Executes the appropriate step based on the current event.
    /// </summary>
    /// <param name="eventId">The current event ID</param>
    /// <param name="processModel">The process model containing state</param>
    /// <returns>The next event ID to transition to</returns>
    private async Task<string> ExecuteStepByEvent(string eventId, ProcessModel processModel)
    {
        return eventId switch
        {
            AgentOrchestrationEvents.StartProcess => await _entityResolutionStep.ExecuteAsync(processModel),
            AgentOrchestrationEvents.EntityExtractionCompleted => await _documentSearchStep.ExecuteAsync(processModel),
            AgentOrchestrationEvents.DocumentSearchCompleted => await _responseGenerationStep.ExecuteAsync(processModel),
            AgentOrchestrationEvents.ResponseGenerationCompleted => await _userResponseStep.ExecuteAsync(processModel),
            AgentOrchestrationEvents.UserClarificationNeeded => await _userResponseStep.ExecuteAsync(processModel),
            AgentOrchestrationEvents.ProcessError => await _userResponseStep.ExecuteAsync(processModel),
            _ => throw new InvalidOperationException($"Unknown event ID: {eventId}")
        };
    }

    /// <summary>
    /// Creates a ChatResponse from the final ProcessModel state.
    /// </summary>
    /// <param name="request">The original chat request</param>
    /// <param name="processModel">The completed process model</param>
    /// <returns>The final chat response</returns>
    private static ChatResponse CreateChatResponse(ChatRequest request, ProcessModel processModel)
    {
        return new ChatResponse
        {
            ThreadId = request.ThreadId,
            Response = processModel.GeneratedResponse?.Answer ?? processModel.ClarificationMessage ?? "No response generated.",
            ExtractedEntities = processModel.ExtractedEntities.ConvertAll(e => e.Text),
            RelevantDocuments = processModel.RelevantDocuments
        };
    }

    /// <summary>
    /// Creates an error response when orchestration fails.
    /// </summary>
    /// <param name="request">The original chat request</param>
    /// <returns>An error chat response</returns>
    private static ChatResponse CreateErrorResponse(ChatRequest request)
    {
        return new ChatResponse
        {
            ThreadId = request.ThreadId,
            Response = "I encountered an error while processing your request. Please try again.",
            ExtractedEntities = [],
            RelevantDocuments = []
        };
    }
}