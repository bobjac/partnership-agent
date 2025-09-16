using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles entity extraction from user input using the EntityResolutionAgent.
/// </summary>
public class EntityResolutionStep : KernelProcessStep
{
    private readonly IEntityResolutionAgent _entityResolutionAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly ILogger<EntityResolutionStep> _logger;

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
    {
        _entityResolutionAgent = entityResolutionAgent ?? throw new ArgumentNullException(nameof(entityResolutionAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            _logger.LogInformation("Starting entity extraction for session {SessionId}", processModel.SessionId);
            
            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Analyzing your question..." }));

            // Extract entities from the input
            var entities = await _entityResolutionAgent.ExtractEntitiesAsync(processModel.Input);
            processModel.ExtractedEntities = entities.ToList();

            _logger.LogInformation("Extracted {EntityCount} entities for session {SessionId}", 
                entities.Count(), processModel.SessionId);

            // Send status update with extracted entities
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Extracted {entities.Count()} entities from your query",
                    entities = entities.Select(e => e.Text).ToList()
                }));

            // Always proceed to document search - we can handle queries even without explicit entities
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.EntityExtractionCompleted,
                Data = processModel
            });
        }
        catch (Exception ex) when (LogError(ex, $"Error extracting entities for session {processModel.SessionId}"))
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