using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;

#pragma warning disable SKEXP0080

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles scoping evaluation to determine if a user request is in scope
/// for the partnership agent system. Out-of-scope requests (like jokes) are rejected early.
/// </summary>
public class ScopingStep : KernelProcessStep
{
    private readonly ScopingAgent _scopingAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly ILogger<ScopingStep> _logger;

    /// <summary>
    /// Constructor for ScopingStep.
    /// </summary>
    /// <param name="scopingAgent">Agent for evaluating request scope</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public ScopingStep(
        ScopingAgent scopingAgent,
        IBidirectionalToClientChannel responseChannel,
        ILogger<ScopingStep> logger)
    {
        _scopingAgent = scopingAgent ?? throw new ArgumentNullException(nameof(scopingAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Evaluates the scope of a user request and emits appropriate events.
    /// If the request is out of scope, the process terminates early with a message.
    /// If in scope, the process continues to entity extraction.
    /// </summary>
    /// <param name="context">The KernelProcessStepContext that exposes framework services</param>
    /// <param name="kernel">SemanticKernel Kernel object</param>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Task representing the asynchronous operation</returns>
    [KernelFunction]
    [Description("Evaluates if the user request is in scope for the partnership agent system")]
    public async Task EvaluateScopeAsync(KernelProcessStepContext context, Kernel kernel, ProcessModel processModel)
    {
        try
        {
            _logger.LogInformation("Starting scope evaluation for session {ThreadId}", processModel.ThreadId);

            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status,
                JsonSerializer.Serialize(new { message = "Checking if your request is in scope..." }));

            // Evaluate the scope using the ScopingAgent
            var scopingResult = await _scopingAgent.EvaluateScopeAsync(processModel.Input);
            processModel.ScopingResult = scopingResult;

            _logger.LogInformation("Scope evaluation complete for session {ThreadId}: IsInScope={IsInScope}, Category={Category}, Confidence={Confidence}",
                processModel.ThreadId, scopingResult.IsInScope, scopingResult.Category, scopingResult.ConfidenceLevel);

            if (!scopingResult.IsInScope)
            {
                // Request is out of scope - terminate the process with an appropriate message
                _logger.LogInformation("Request is out of scope for session {ThreadId}, terminating process", processModel.ThreadId);

                // Prepare out-of-scope message
                var outOfScopeMessage = scopingResult.OutOfScopeMessage ??
                    "I apologize, but I'm designed to help with partnership agreements, contracts, and business relationships. " +
                    "Your request appears to be outside my area of expertise.";

                // Add suggestions if available
                if (scopingResult.Suggestions?.Count > 0)
                {
                    outOfScopeMessage += "\n\nHere are some examples of what I can help with:\n";
                    foreach (var suggestion in scopingResult.Suggestions)
                    {
                        outOfScopeMessage += $"• {suggestion}\n";
                    }
                }

                processModel.NeedsClarification = true;
                processModel.ClarificationMessage = outOfScopeMessage;

                // Send the out-of-scope message to the client
                await _responseChannel.WriteAsync(AIEventTypes.Chat, outOfScopeMessage);

                // Emit event to skip to user response (which will terminate the process)
                await context.EmitEventAsync(new KernelProcessEvent
                {
                    Id = AgentOrchestrationEvents.OutOfScope,
                    Data = processModel
                });
            }
            else
            {
                // Request is in scope - proceed to entity extraction
                _logger.LogInformation("Request is in scope for session {ThreadId}, proceeding to entity extraction", processModel.ThreadId);

                await _responseChannel.WriteAsync(AIEventTypes.Status,
                    JsonSerializer.Serialize(new { message = "Request is in scope, proceeding with analysis..." }));

                await context.EmitEventAsync(new KernelProcessEvent
                {
                    Id = AgentOrchestrationEvents.ScopingCompleted,
                    Data = processModel
                });
            }
        }
        catch (Exception ex) when (LogError(ex, $"Error evaluating scope for session {processModel.ThreadId}"))
        {
            // On error, default to in-scope to avoid blocking legitimate requests
            _logger.LogWarning("Scoping evaluation failed for session {ThreadId}, defaulting to in-scope", processModel.ThreadId);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ScopingCompleted,
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
        _logger.LogError(ex, "Scoping step error: {ErrorMessage}", message);
        return true;
    }
}
