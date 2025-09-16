using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Process;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Steps;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Services;

/// <summary>
/// Service that orchestrates the execution of partnership agent steps using Semantic Kernel's process framework.
/// This provides controlled flow, auditability, and early termination capabilities.
/// </summary>
public class StepOrchestrationService
{
    private static readonly ActivitySource _activitySource = new("PartnershipAgent.StepOrchestration");
    private readonly Kernel _kernel;
    private readonly ILogger<StepOrchestrationService> _logger;

    /// <summary>
    /// Constructor for StepOrchestrationService.
    /// </summary>
    public StepOrchestrationService(
        Kernel kernel,
        ILogger<StepOrchestrationService> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a chat request through the orchestrated step pipeline using Semantic Kernel's process framework.
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

            // Build and execute the process using Semantic Kernel's native process framework
            var process = BuildProcess(processModel.SessionId);
            
            await using LocalKernelProcessContext localProcess = await process.StartAsync(
                _kernel,
                new KernelProcessEvent()
                {
                    Id = AgentOrchestrationEvents.StartProcess,
                    Data = processModel
                });

            _logger.LogInformation("Step orchestration completed for session {SessionId}", processModel.SessionId);

            activity?.SetTag("orchestration.final_event", AgentOrchestrationEvents.ProcessCompleted);

            return CreateChatResponse(request, processModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in step orchestration for session {SessionId}", processModel.SessionId);
            return CreateErrorResponse(request);
        }
    }

    /// <summary>
    /// Builds the process using Semantic Kernel's native ProcessBuilder.
    /// </summary>
    /// <param name="sessionId">The session ID for the process</param>
    /// <returns>The built kernel process</returns>
    private KernelProcess BuildProcess(Guid sessionId)
    {
        ProcessBuilder processBuilder = new("PartnershipAgent");
        
        // Add steps using the native ProcessBuilder pattern
        var entityResolutionStep = processBuilder.AddStepFromType<EntityResolutionStep>();
        var documentSearchStep = processBuilder.AddStepFromType<DocumentSearchStep>();
        var responseGenerationStep = processBuilder.AddStepFromType<ResponseGenerationStep>();
        var userResponseStep = processBuilder.AddStepFromType<UserResponseStep>();

        // Configure event-driven flow using native process framework
        processBuilder
            .OnInputEvent(AgentOrchestrationEvents.StartProcess)
            .SendEventTo(new ProcessFunctionTargetBuilder(entityResolutionStep, "ExtractEntitiesAsync"));

        entityResolutionStep
            .OnEvent(AgentOrchestrationEvents.EntityExtractionCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(documentSearchStep, "SearchDocumentsAsync"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.DocumentSearchCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(responseGenerationStep, "GenerateResponseAsync"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.UserClarificationNeeded)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.ResponseGenerationCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.UserClarificationNeeded)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        // Error handling - route all errors to user response step
        entityResolutionStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, "SendUserResponseAsync"));

        // Process completion
        userResponseStep
            .OnEvent(AgentOrchestrationEvents.ProcessCompleted)
            .StopProcess();

        return processBuilder.Build();
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