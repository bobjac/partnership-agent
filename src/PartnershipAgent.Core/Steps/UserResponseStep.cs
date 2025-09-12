using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Final step that handles sending the complete response to the user.
/// </summary>
public class UserResponseStep : BaseKernelProcessStep
{
    /// <summary>
    /// Constructor for UserResponseStep.
    /// </summary>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public UserResponseStep(
        IBidirectionalToClientChannel responseChannel, 
        ILogger<UserResponseStep> logger) 
        : base(responseChannel, logger)
    {
    }

    /// <summary>
    /// Executes final response assembly and delivery to the user.
    /// </summary>
    /// <param name="processModel">The process model containing all accumulated data</param>
    /// <returns>Event ID indicating process completion</returns>
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        try
        {
            Logger.LogInformation("Sending final response for session {SessionId}", processModel.SessionId);

            if (processModel.NeedsClarification)
            {
                // Send clarification message
                await ResponseChannel.WriteAsync(AIEventTypes.Chat, 
                    JsonSerializer.Serialize(new TextAgentResponse(processModel.ClarificationMessage)));
                
                Logger.LogInformation("Sent clarification request for session {SessionId}", processModel.SessionId);
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
                        sessionId = processModel.SessionId,
                        processedAt = DateTime.UtcNow,
                        documentsFound = processModel.RelevantDocuments.Count,
                        entitiesExtracted = processModel.ExtractedEntities.Count
                    }
                };

                await ResponseChannel.WriteAsync(AIEventTypes.Chat, JsonSerializer.Serialize(fullResponse));
                
                Logger.LogInformation("Sent complete structured response for session {SessionId}", processModel.SessionId);
            }
            else
            {
                // Fallback response
                var fallbackResponse = new TextAgentResponse(
                    "I was unable to process your request completely. Please try again with a more specific question about partnership agreements.");
                
                await ResponseChannel.WriteAsync(AIEventTypes.Chat, JsonSerializer.Serialize(fallbackResponse));
                
                Logger.LogWarning("Sent fallback response for session {SessionId}", processModel.SessionId);
            }

            // Send completion event
            await ResponseChannel.WriteAsync(AIEventTypes.Completion, 
                JsonSerializer.Serialize(new { 
                    sessionId = processModel.SessionId, 
                    timestamp = DateTime.UtcNow,
                    success = !processModel.NeedsClarification,
                    totalStepsCompleted = GetCompletedStepsCount(processModel)
                }));

            return AgentOrchestrationEvents.ProcessCompleted;
        }
        catch (Exception ex) when (LogError(ex, $"Error sending response for session {processModel.SessionId}"))
        {
            // Send error response as last resort
            await ResponseChannel.WriteAsync(AIEventTypes.Error, 
                JsonSerializer.Serialize(new { 
                    message = "An error occurred while preparing your response. Please try again.",
                    sessionId = processModel.SessionId 
                }));

            return AgentOrchestrationEvents.ProcessError;
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
        Logger.LogError(ex, "User response step error: {ErrorMessage}", message);
        return true;
    }
}