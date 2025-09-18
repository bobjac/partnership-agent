using System;
using System.Collections.Generic;
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
/// Step that handles document search using the FAQAgent's search capabilities.
/// </summary>
public class DocumentSearchStep : KernelProcessStep
{
    private readonly IFAQAgent _faqAgent;
    private readonly IBidirectionalToClientChannel _responseChannel;
    private readonly ILogger<DocumentSearchStep> _logger;

    /// <summary>
    /// Constructor for DocumentSearchStep.
    /// </summary>
    /// <param name="faqAgent">Agent for searching documents</param>
    /// <param name="responseChannel">Channel for sending responses to the client</param>
    /// <param name="logger">Logger instance for this step</param>
    public DocumentSearchStep(
        IFAQAgent faqAgent,
        IBidirectionalToClientChannel responseChannel, 
        ILogger<DocumentSearchStep> logger)
    {
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
        _responseChannel = responseChannel ?? throw new ArgumentNullException(nameof(responseChannel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes document search and emits appropriate events based on the result.
    /// </summary>
    /// <param name="context">The KernelProcessStepContext that exposes framework services</param>
    /// <param name="kernel">SemanticKernel Kernel object</param>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Task representing the asynchronous operation</returns>
    [KernelFunction]
    [Description("Searches for relevant documents based on user input")]
    public async Task SearchDocumentsAsync(KernelProcessStepContext context, Kernel kernel, ProcessModel processModel)
    {
        try
        {
            _logger.LogInformation("Starting document search for session {ThreadId}", processModel.ThreadId);
            
            // Send status update to client
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Searching for relevant documents..." }));

            // Search for relevant documents
            var allowedCategories = GetAllowedCategoriesForTenant(processModel.TenantId);
            var documents = await _faqAgent.SearchDocumentsAsync(processModel.Input, processModel.TenantId, allowedCategories);
            processModel.RelevantDocuments = documents;

            _logger.LogInformation("Found {DocumentCount} relevant documents for session {ThreadId}", 
                documents.Count, processModel.ThreadId);

            // Send status update with search results
            await _responseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Found {documents.Count} relevant documents",
                    documentTitles = documents.Select(d => d.Title).ToList()
                }));

            // Check if we found any relevant documents
            if (!documents.Any())
            {
                processModel.NeedsClarification = true;
                processModel.ClarificationMessage = "I couldn't find any relevant documents for your question. Could you please rephrase your question or provide more specific details about partnership agreements?";
                
                await context.EmitEventAsync(new KernelProcessEvent
                {
                    Id = AgentOrchestrationEvents.UserClarificationNeeded,
                    Data = processModel
                });
                return;
            }

            // Proceed to response generation
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.DocumentSearchCompleted,
                Data = processModel
            });
        }
        catch (Exception ex) when (LogError(ex, $"Error searching documents for session {processModel.ThreadId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while searching for relevant documents. Please try again.";
            
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = AgentOrchestrationEvents.ProcessError,
                Data = processModel
            });
        }
    }

    /// <summary>
    /// Gets the allowed document categories for a given tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier</param>
    /// <returns>List of allowed document categories</returns>
    private static List<string> GetAllowedCategoriesForTenant(string tenantId)
    {
        // In a real implementation, this would be based on tenant permissions
        return ["templates", "guidelines", "policies", "contracts"];
    }

    /// <summary>
    /// Logs an error with a consistent message template.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">The error message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogError(Exception ex, string message)
    {
        _logger.LogError(ex, "Document search step error: {ErrorMessage}", message);
        return true;
    }

    /// <summary>
    /// Legacy method for backward compatibility - should not be used with process framework
    /// </summary>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Event ID indicating the next step or error condition</returns>
    [Obsolete("Use SearchDocumentsAsync with KernelProcessStepContext instead")]
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        throw new NotSupportedException("Use SearchDocumentsAsync with KernelProcessStepContext instead");
    }
}