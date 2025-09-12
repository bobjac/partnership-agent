using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles entity extraction from user input using the EntityResolutionAgent.
/// </summary>
public class EntityResolutionStep : BaseKernelProcessStep
{
    private readonly IEntityResolutionAgent _entityResolutionAgent;

    /// <summary>
    /// Constructor for EntityResolutionStep.
    /// </summary>
    /// <param name="entityResolutionAgent">Agent for extracting entities from user input</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public EntityResolutionStep(
        IEntityResolutionAgent entityResolutionAgent,
        IBidirectionalToClientChannel responseChannel, 
        ILogger<EntityResolutionStep> logger) 
        : base(responseChannel, logger)
    {
        _entityResolutionAgent = entityResolutionAgent ?? throw new ArgumentNullException(nameof(entityResolutionAgent));
    }

    /// <summary>
    /// Executes entity extraction and emits appropriate events based on the result.
    /// </summary>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Event ID indicating the next step or error condition</returns>
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        try
        {
            Logger.LogInformation("Starting entity extraction for session {SessionId}", processModel.SessionId);
            
            // Send status update to client
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Analyzing your question..." }));

            // Extract entities from the input
            var entities = await _entityResolutionAgent.ExtractEntitiesAsync(processModel.Input);
            processModel.ExtractedEntities = entities.ToList();

            Logger.LogInformation("Extracted {EntityCount} entities for session {SessionId}", 
                entities.Count(), processModel.SessionId);

            // Send status update with extracted entities
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Extracted {entities.Count()} entities from your query",
                    entities = entities.Select(e => e.Text).ToList()
                }));

            // Always proceed to document search - we can handle queries even without explicit entities
            return AgentOrchestrationEvents.EntityExtractionCompleted;
        }
        catch (Exception ex) when (LogError(ex, $"Error extracting entities for session {processModel.SessionId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while analyzing your request. Please try rephrasing your question.";
            
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
        Logger.LogError(ex, "Entity resolution step error: {ErrorMessage}", message);
        return true;
    }
}