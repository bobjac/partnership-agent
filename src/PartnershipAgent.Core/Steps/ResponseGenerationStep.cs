using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Evaluation;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles generating structured responses using the FAQAgent.
/// </summary>
public class ResponseGenerationStep : KernelProcessStep
{
    private readonly FAQAgent _faqAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly IChatHistoryService _chatHistoryService;
    private readonly ILogger<ResponseGenerationStep> _logger;
    private readonly IAssistantResponseEvaluator? _evaluator;

    /// <summary>
    /// Constructor for ResponseGenerationStep.
    /// </summary>
    /// <param name="faqAgent">Agent for generating responses</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public ResponseGenerationStep(
        FAQAgent faqAgent,
        IBidirectionalToClientChannel responseChannel,
        IChatHistoryService chatHistoryService,
        ILogger<ResponseGenerationStep> logger,
        IAssistantResponseEvaluator? evaluator = null)
    {
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _evaluator = evaluator;
    }

    /// <summary>
    /// Executes response generation and emits appropriate events based on the result.
    /// </summary>
    /// <param name="context">The KernelProcessStepContext that exposes framework services</param>
    /// <param name="kernel">SemanticKernel Kernel object</param>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Task representing the asynchronous operation</returns>
    [KernelFunction]
    [Description("Generates structured responses based on relevant documents")]
    public async Task GenerateResponseAsync(KernelProcessStepContext context, Kernel kernel, ProcessModel processModel)
    {
        try
        {
            _logger.LogInformation("Starting response generation for session {ThreadId}", processModel.ThreadId);
            
            // Get the streaming channel from the kernel services (which contains the one we injected)
            var streamingChannel = kernel.GetRequiredService<IBidirectionalToClientChannel>();
            _logger.LogInformation("RESPONSEGEN: Got streaming channel (not null: {NotNull}, type: {Type}) for thread {ThreadId}", 
                streamingChannel != null, streamingChannel?.GetType().Name, processModel.ThreadId);
            
            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Generating comprehensive answer..." }));

            // Generate structured response based on found documents with streaming support
            _logger.LogInformation("RESPONSEGEN: Calling GenerateStructuredResponseAsync with streamingChannel (not null: {NotNull}) for thread {ThreadId}", 
                streamingChannel != null, processModel.ThreadId);
            var structuredResponse = await _faqAgent.GenerateStructuredResponseAsync(processModel.Input, processModel.RelevantDocuments, streamingChannel);
            processModel.GeneratedResponse = structuredResponse;
            processModel.FinalResponse = structuredResponse.Answer;
            await _chatHistoryService.AddMessageToChatHistoryAsync(processModel.ThreadId, new ChatMessageContent(AuthorRole.Assistant, processModel.FinalResponse));

            _logger.LogInformation("Generated response with confidence {Confidence} for session {ThreadId}", 
                structuredResponse.ConfidenceLevel, processModel.ThreadId);

            // Evaluate the response quality if evaluator is available (fire-and-forget for performance)
            if (_evaluator != null && !string.IsNullOrWhiteSpace(structuredResponse.Answer))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var activity = System.Diagnostics.Activity.Current?.Source?.StartActivity("FAQAgent Evaluation");
                        _ = await _evaluator.EvaluateAndLogAsync(
                            userPrompt: processModel.Input,
                            response: structuredResponse.Answer,
                            module: "FAQAgent",
                            parentActivity: activity?.Source,
                            expectedAnswer: null,
                            retrievedDocuments: processModel.RelevantDocuments
                        );
                    }
                    catch (Exception evalEx)
                    {
                        _logger.LogWarning(evalEx, "Failed to evaluate FAQ response for session {ThreadId}", processModel.ThreadId);
                    }
                });
            }

            // Send status update with response metadata
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = "Response generated successfully",
                    confidence = structuredResponse.ConfidenceLevel,
                    hasCompleteAnswer = structuredResponse.HasCompleteAnswer,
                    sourceCount = structuredResponse.SourceDocuments.Count
                }));

            // Check if the response needs clarification based on confidence level
            if (structuredResponse.ConfidenceLevel == "low" && !structuredResponse.HasCompleteAnswer)
            {
                processModel.NeedsClarification = true;
                processModel.ClarificationMessage = structuredResponse.Answer + 
                    " Could you provide more specific details about what aspect of partnership agreements you're interested in?";
                
                await context.EmitEventAsync(new KernelProcessEvent
                {
                    Id = AgentOrchestrationEvents.UserClarificationNeeded,
                    Data = processModel
                });
                return;
            }

            // Send the structured response data to the client for processing
            await _responseChannel.WriteAsync(AIEventTypes.Chat, 
                JsonSerializer.Serialize(new { 
                    answer = structuredResponse.Answer,
                    confidence = structuredResponse.ConfidenceLevel,
                    sources = structuredResponse.SourceDocuments,
                    followUpSuggestions = structuredResponse.FollowUpSuggestions,
                    hasCompleteAnswer = structuredResponse.HasCompleteAnswer
                }));

            // Proceed to final user response step
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ResponseGenerationCompleted,
                Data = processModel
            });
        }
        catch (Exception ex) when (LogError(ex, $"Error generating response for session {processModel.ThreadId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while generating your response. Please try again.";
            
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
        _logger.LogError(ex, "Response generation step error: {ErrorMessage}", message);
        return true;
    }

    /// <summary>
    /// Legacy method for backward compatibility - should not be used with process framework
    /// </summary>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Event ID indicating the next step or error condition</returns>
    [Obsolete("Use GenerateResponseAsync with KernelProcessStepContext instead")]
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        throw new NotSupportedException("Use GenerateResponseAsync with KernelProcessStepContext instead");
    }
}