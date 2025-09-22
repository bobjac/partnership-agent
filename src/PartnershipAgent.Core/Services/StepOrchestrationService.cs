using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Process;
using PartnershipAgent.Core.Agents;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StepOrchestrationService> _logger;

    /// <summary>
    /// Constructor for StepOrchestrationService.
    /// </summary>
    public StepOrchestrationService(
        Kernel kernel,
        IServiceProvider serviceProvider,
        ILogger<StepOrchestrationService> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

        using var activity = _activitySource.StartActivity($"ThreadId: {request.ThreadId}", ActivityKind.Internal, parentId: default);
        
        var processModel = new ProcessModel
        {
            ThreadId = Guid.Parse(request.ThreadId),
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

            _logger.LogInformation("Starting step orchestration for session {ThreadId}", processModel.ThreadId);

            // Build and execute the process using Semantic Kernel's native process framework
            var process = BuildProcess(processModel.ThreadId);
            
            // Create a kernel builder and register the services (following working pattern)
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<EntityResolutionAgent>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<FAQAgent>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<IBidirectionalToClientChannel>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<ProcessResponseCollector>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<ILogger<EntityResolutionStep>>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<ILogger<DocumentSearchStep>>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<ILogger<ResponseGenerationStep>>());
            kernelBuilder.Services.AddSingleton(_serviceProvider.GetRequiredService<ILogger<UserResponseStep>>());
            
            // Copy the chat completion service from the original kernel
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            kernelBuilder.Services.AddSingleton(chatCompletion);
            
            var processKernel = kernelBuilder.Build();
            
            await using LocalKernelProcessContext localProcess = await process.StartAsync(
                processKernel,
                new KernelProcessEvent()
                {
                    Id = AgentOrchestrationEvents.StartProcess,
                    Data = processModel
                });

            _logger.LogInformation("Step orchestration completed for session {ThreadId}", processModel.ThreadId);

            activity?.SetTag("orchestration.final_event", AgentOrchestrationEvents.ProcessCompleted);

            // Get the final response from the collector
            var responseCollector = _serviceProvider.GetRequiredService<ProcessResponseCollector>();
            var finalResponse = responseCollector.GetAndRemoveResponse(processModel.ThreadId);
            
            return finalResponse ?? CreateChatResponse(request, processModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in step orchestration for session {ThreadId}", processModel.ThreadId);
            return CreateErrorResponse(request);
        }
    }

    /// <summary>
    /// Builds the process using Semantic Kernel's native ProcessBuilder.
    /// </summary>
    /// <param name="ThreadId">The session ID for the process</param>
    /// <returns>The built kernel process</returns>
    private KernelProcess BuildProcess(Guid ThreadId)
    {
        ProcessBuilder processBuilder = new("PartnershipAgent");
        
        // Add steps using the native ProcessBuilder pattern
        var entityResolutionStep = processBuilder.AddStepFromType<EntityResolutionStep>();
        var documentSearchStep = processBuilder.AddStepFromType<DocumentSearchStep>();
        var responseGenerationStep = processBuilder.AddStepFromType<ResponseGenerationStep>();
        var userResponseStep = processBuilder.AddStepFromType<UserResponseStep>();

        // Configure event-driven flow using ProcessFunctionTargetBuilder (fixed based on working example)
        processBuilder
            .OnInputEvent(AgentOrchestrationEvents.StartProcess)
            .SendEventTo(new ProcessFunctionTargetBuilder(entityResolutionStep, parameterName: "processModel"));

        entityResolutionStep
            .OnEvent(AgentOrchestrationEvents.EntityExtractionCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(documentSearchStep, parameterName: "processModel"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.DocumentSearchCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(responseGenerationStep, parameterName: "processModel"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.UserClarificationNeeded)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.ResponseGenerationCompleted)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.UserClarificationNeeded)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

        // Error handling - route all errors to user response step
        entityResolutionStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

        documentSearchStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

        responseGenerationStep
            .OnEvent(AgentOrchestrationEvents.ProcessError)
            .SendEventTo(new ProcessFunctionTargetBuilder(userResponseStep, parameterName: "processModel"));

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