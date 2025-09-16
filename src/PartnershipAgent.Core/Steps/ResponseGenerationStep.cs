using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles generating structured responses using the FAQAgent.
/// </summary>
public class ResponseGenerationStep : KernelProcessStep
{
    private readonly IFAQAgent _faqAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly ILogger<ResponseGenerationStep> _logger;

    /// <summary>
    /// Constructor for ResponseGenerationStep.
    /// </summary>
    /// <param name="faqAgent">Agent for generating responses</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public ResponseGenerationStep(
        IFAQAgent faqAgent,
        IBidirectionalToClientChannel responseChannel, 
        ILogger<ResponseGenerationStep> logger)
    {
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogInformation("Starting response generation for session {SessionId}", processModel.SessionId);
            
            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Generating comprehensive answer..." }));

            // Generate structured response based on found documents
            var structuredResponse = await _faqAgent.GenerateStructuredResponseAsync(processModel.Input, processModel.RelevantDocuments);
            processModel.GeneratedResponse = structuredResponse;
            processModel.FinalResponse = structuredResponse.Answer;

            _logger.LogInformation("Generated response with confidence {Confidence} for session {SessionId}", 
                structuredResponse.ConfidenceLevel, processModel.SessionId);

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
        catch (Exception ex) when (LogError(ex, $"Error generating response for session {processModel.SessionId}"))
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