using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Final step that handles sending the complete response to the user.
/// </summary>
public class UserResponseStep : KernelProcessStep
{
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly ProcessResponseCollector _responseCollector;
    private readonly ILogger<UserResponseStep> _logger;

    /// <summary>
    /// Constructor for UserResponseStep.
    /// </summary>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="responseCollector">Collector for storing final responses</param>
    /// <param name="logger">Logger instance for this step</param>
    public UserResponseStep(
        IBidirectionalToClientChannel responseChannel,
        ProcessResponseCollector responseCollector,
        ILogger<UserResponseStep> logger)
    {
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _responseCollector = responseCollector ?? throw new ArgumentNullException(nameof(responseCollector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes final response assembly and delivery to the user.
    /// </summary>
    /// <param name="context">The KernelProcessStepContext that exposes framework services</param>
    /// <param name="kernel">SemanticKernel Kernel object</param>
    /// <param name="processModel">The process model containing all accumulated data</param>
    /// <returns>Task representing the asynchronous operation</returns>
    [KernelFunction]
    [Description("Sends the final response to the user and completes the process")]
    public async Task SendUserResponseAsync(KernelProcessStepContext context, Kernel kernel, ProcessModel processModel)
    {
        try
        {
            _logger.LogInformation("Sending final response for session {ThreadId}", processModel.ThreadId);

            if (processModel.NeedsClarification)
            {
                // Send clarification message
                await _responseChannel.WriteAsync(AIEventTypes.Chat, 
                    JsonSerializer.Serialize(new TextAgentResponse(processModel.ClarificationMessage)));
                
                // Store the clarification response
                var chatResponse = new ChatResponse
                {
                    Response = processModel.ClarificationMessage,
                    ExtractedEntities = processModel.ExtractedEntities.ConvertAll(e => e.Text),
                    RelevantDocuments = processModel.RelevantDocuments
                };
                _responseCollector.SetResponse(processModel.ThreadId, chatResponse);
                
                _logger.LogInformation("Sent clarification request for session {ThreadId}", processModel.ThreadId);
            }
            else if (processModel.GeneratedResponse != null)
            {
                // Send the complete structured response
                var fullResponse = new
                {
                    answer = processModel.GeneratedResponse.Answer,
                    confidence = processModel.GeneratedResponse.ConfidenceLevel,
                    hasCompleteAnswer = processModel.GeneratedResponse.HasCompleteAnswer,
                    sources = processModel.GeneratedResponse.SourceDocuments,
                    followUpSuggestions = processModel.GeneratedResponse.FollowUpSuggestions,
                    extractedEntities = processModel.ExtractedEntities.Select(e => e.Text).ToList(),
                    relevantDocuments = processModel.RelevantDocuments.Select(d => new
                    {
                        title = d.Title,
                        category = d.Category,
                        score = d.Score
                    }).ToList(),
                    metadata = new
                    {
                        ThreadId = processModel.ThreadId,
                        processedAt = DateTime.UtcNow,
                        documentsFound = processModel.RelevantDocuments.Count,
                        entitiesExtracted = processModel.ExtractedEntities.Count
                    }
                };

                await _responseChannel.WriteAsync(AIEventTypes.Chat, JsonSerializer.Serialize(fullResponse));
                
                // Store the final response for the StepOrchestrationService to retrieve
                var chatResponse = new ChatResponse
                {
                    Response = processModel.GeneratedResponse.Answer,
                    ExtractedEntities = processModel.ExtractedEntities.ConvertAll(e => e.Text),
                    RelevantDocuments = processModel.RelevantDocuments
                };
                _responseCollector.SetResponse(processModel.ThreadId, chatResponse);
                
                _logger.LogInformation("Sent complete structured response for session {ThreadId}", processModel.ThreadId);
            }
            else
            {
                // Fallback response
                var fallbackResponse = new TextAgentResponse(
                    "I was unable to process your request completely. Please try again with a more specific question about partnership agreements.");
                
                await _responseChannel.WriteAsync(AIEventTypes.Chat, JsonSerializer.Serialize(fallbackResponse));
                
                // Store the fallback response
                var chatResponse = new ChatResponse
                {
                    Response = fallbackResponse.Content,
                    ExtractedEntities = processModel.ExtractedEntities.ConvertAll(e => e.Text),
                    RelevantDocuments = processModel.RelevantDocuments
                };
                _responseCollector.SetResponse(processModel.ThreadId, chatResponse);
                
                _logger.LogWarning("Sent fallback response for session {ThreadId}", processModel.ThreadId);
            }

            // Send completion event
            await _responseChannel.WriteAsync(AIEventTypes.Completion, 
                JsonSerializer.Serialize(new { 
                    ThreadId = processModel.ThreadId, 
                    timestamp = DateTime.UtcNow,
                    success = !processModel.NeedsClarification,
                    totalStepsCompleted = GetCompletedStepsCount(processModel)
                }));

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ProcessCompleted,
                Data = processModel
            });
        }
        catch (Exception ex) when (LogError(ex, $"Error sending response for session {processModel.ThreadId}"))
        {
            // Send error response as last resort
            await _responseChannel.WriteAsync(AIEventTypes.Error, 
                JsonSerializer.Serialize(new { 
                    message = "An error occurred while preparing your response. Please try again.",
                    ThreadId = processModel.ThreadId 
                }));

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ProcessError,
                Data = processModel
            });
        }
    }

    /// <summary>
    /// Gets the count of completed steps for reporting purposes.
    /// </summary>
    /// <param name="processModel">The process model to analyze</param>
    /// <returns>Number of completed steps</returns>
    private static int GetCompletedStepsCount(ProcessModel processModel)
    {
        var completedSteps = 0;
        
        if (processModel.ExtractedEntities.Any()) completedSteps++;
        if (processModel.RelevantDocuments.Any()) completedSteps++;
        if (processModel.GeneratedResponse != null) completedSteps++;
        if (!string.IsNullOrEmpty(processModel.FinalResponse)) completedSteps++;

        return completedSteps;
    }

    /// <summary>
    /// Logs an error with a consistent message template.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">The error message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogError(Exception ex, string message)
    {
        _logger.LogError(ex, "User response step error: {ErrorMessage}", message);
        return true;
    }

    /// <summary>
    /// Legacy method for backward compatibility - should not be used with process framework
    /// </summary>
    /// <param name="processModel">The process model containing all accumulated data</param>
    /// <returns>Event ID indicating process completion</returns>
    [Obsolete("Use SendUserResponseAsync with KernelProcessStepContext instead")]
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        throw new NotSupportedException("Use SendUserResponseAsync with KernelProcessStepContext instead");
    }
}