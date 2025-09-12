using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PartnershipAgent.Core.Agents;
using PartnershipAgent.Core.Models;

namespace PartnershipAgent.Core.Steps;

/// <summary>
/// Step that handles document search using the FAQAgent's search capabilities.
/// </summary>
public class DocumentSearchStep : BaseKernelProcessStep
{
    private readonly IFAQAgent _faqAgent;

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
        : base(responseChannel, logger)
    {
        _faqAgent = faqAgent ?? throw new ArgumentNullException(nameof(faqAgent));
    }

    /// <summary>
    /// Executes document search and emits appropriate events based on the result.
    /// </summary>
    /// <param name="processModel">The process model containing session and input details</param>
    /// <returns>Event ID indicating the next step or error condition</returns>
    public async Task<string> ExecuteAsync(ProcessModel processModel)
    {
        try
        {
            Logger.LogInformation("Starting document search for session {SessionId}", processModel.SessionId);
            
            // Send status update to client
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { message = "Searching for relevant documents..." }));

            // Search for relevant documents
            var allowedCategories = GetAllowedCategoriesForTenant(processModel.TenantId);
            var documents = await _faqAgent.SearchDocumentsAsync(processModel.Input, processModel.TenantId, allowedCategories);
            processModel.RelevantDocuments = documents;

            Logger.LogInformation("Found {DocumentCount} relevant documents for session {SessionId}", 
                documents.Count, processModel.SessionId);

            // Send status update with search results
            await ResponseChannel.WriteAsync(AIEventTypes.Status, 
                JsonSerializer.Serialize(new { 
                    message = $"Found {documents.Count} relevant documents",
                    documentTitles = documents.Select(d => d.Title).ToList()
                }));

            // Check if we found any relevant documents
            if (!documents.Any())
            {
                processModel.NeedsClarification = true;
                processModel.ClarificationMessage = "I couldn't find any relevant documents for your question. Could you please rephrase your question or provide more specific details about partnership agreements?";
                
                return AgentOrchestrationEvents.UserClarificationNeeded;
            }

            // Proceed to response generation
            return AgentOrchestrationEvents.DocumentSearchCompleted;
        }
        catch (Exception ex) when (LogError(ex, $"Error searching documents for session {processModel.SessionId}"))
        {
            processModel.NeedsClarification = true;
            processModel.ClarificationMessage = "I encountered an error while searching for relevant documents. Please try again.";
            
            return AgentOrchestrationEvents.ProcessError;
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
        Logger.LogError(ex, "Document search step error: {ErrorMessage}", message);
        return true;
    }
}