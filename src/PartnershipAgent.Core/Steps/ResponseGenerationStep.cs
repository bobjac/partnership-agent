using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles generating structured responses using the FAQAgent.
/// </summary>
public class ResponseGenerationStep : BaseKernelProcessStep
{
    private readonly IFAQAgent _faqAgent;

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
        : base(responseChannel, logger)
    {
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
    }

    /// <summary>
    /// Executes response generation and emits appropriate events based on the result.
    /// </summary>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Event ID indicating the next step or error condition</returns>
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        try
        {
            Logger.LogInformation("Starting response generation for session {SessionId}", processModel.SessionId);
            
            // Send status update to client
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Generating comprehensive answer..." }));

            // Generate structured response based on found documents
            var structuredResponse = await _faqAgent.GenerateStructuredResponseAsync(processModel.Input, processModel.RelevantDocuments);
            processModel.GeneratedResponse = structuredResponse;
            processModel.FinalResponse = structuredResponse.Answer;

            Logger.LogInformation("Generated response with confidence {Confidence} for session {SessionId}", 
                structuredResponse.ConfidenceLevel, processModel.SessionId);

            // Send status update with response metadata
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
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
                
                return AgentOrchestrationEvents.UserClarificationNeeded;
            }

            // Send the structured response data to the client for processing
            await ResponseChannel.WriteAsync(AIEventTypes.Chat, 
                JsonSerializer.Serialize(new { 
                    answer = structuredResponse.Answer,
                    confidence = structuredResponse.ConfidenceLevel,
                    sources = structuredResponse.SourceDocuments,
                    followUpSuggestions = structuredResponse.FollowUpSuggestions,
                    hasCompleteAnswer = structuredResponse.HasCompleteAnswer
                }));

            // Proceed to final user response step
            return AgentOrchestrationEvents.ResponseGenerationCompleted;
        }
        catch (Exception ex) when (LogError(ex, $"Error generating response for session {processModel.SessionId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while generating your response. Please try again.";
            
            return AgentOrchestrationEvents.ProcessError;
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
        Logger.LogError(ex, "Response generation step error: {ErrorMessage}", message);
        return true;
    }
}