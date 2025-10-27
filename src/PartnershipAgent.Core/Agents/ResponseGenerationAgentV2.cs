using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using PartnershipAgent.Core.Models;
using PartnershipAgent.Core.Services;
using PartnershipAgent.Core.Steps;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// Agent Framework (v2) implementation for response generation functionality.
/// Specialized agent that generates comprehensive answers based on partnership documents.
/// Split from original FAQAgent to separate concerns.
/// Supports real-time streaming for better user experience.
/// </summary>
public class ResponseGenerationAgentV2 : BaseAgent
{
    private readonly IChatClient _chatClient;
    private readonly ICitationService _citationService;
    private readonly IChatHistoryService _chatHistoryService;

    /// <summary>
    /// The name of the agent used for identification in the Agent Framework system.
    /// </summary>
    public override string Name => "ResponseGenerationAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that generates comprehensive answers based on partnership documents with citations";

    /// <summary>
    /// Constructor for the ResponseGenerationAgentV2.
    /// Initializes the agent with required dependencies.
    /// </summary>
    public ResponseGenerationAgentV2(
        Guid threadId,
        IChatClient chatClient,
        ICitationService citationService,
        IChatHistoryService chatHistoryService,
        IRequestedBy requestedBy,
        ILogger<ResponseGenerationAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _citationService = citationService ?? throw new ArgumentNullException(nameof(citationService));
        _chatHistoryService = chatHistoryService ?? throw new ArgumentNullException(nameof(chatHistoryService));
        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatClientAgent with the appropriate settings and instructions.
    /// </summary>
    private void InitializeAgent()
    {
        var instructions = @"
            You are a helpful assistant that answers questions about partnership agreements.

            Always respond with:
            - A comprehensive answer based on the available information
            - Your confidence level (high/medium/low) based on document relevance and completeness
            - List of source document titles that were used
            - Detailed citations for each document used, including document ID, title, category, relevant excerpts, and relevance scores
            - Whether you have enough information for a complete answer
            - 2-3 relevant follow-up questions the user might ask

            For Citations, create detailed DocumentCitation objects for each document you reference, including:
            - DocumentId: Use the document's ID
            - DocumentTitle: The document's title
            - Category: The document's category
            - Excerpt: The specific text from the document that supports your answer
            - RelevanceScore: A score from 0.0 to 1.0 indicating how relevant this document is

            If the answer is not in the provided documents, be honest about it in your response.

            Always respond with valid JSON matching the FAQAgentResponse format.
        ";

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });
    }

    /// <summary>
    /// Generates a comprehensive answer based on found documents.
    /// Supports real-time streaming for better user experience.
    /// </summary>
    /// <param name="query">The user's question</param>
    /// <param name="documents">The relevant documents found by search</param>
    /// <param name="streamingChannel">Optional channel for real-time streaming</param>
    /// <returns>Structured response with answer, confidence, and metadata</returns>
    public async Task<FAQAgentResponse> GenerateAnswer(
        string query,
        List<DocumentResult> documents,
        IBidirectionalToClientChannel? streamingChannel = null)
    {
        Logger.LogInformation("Generating structured response based on {Count} documents for conversation thread {ThreadId}", documents.Count, ThreadId);

        try
        {
            var context = string.Join("\n\n", documents.Select(d =>
                $"Document: {d.Title}\nCategory: {d.Category}\nContent: {d.Content}"));

            var hasRelevantInfo = documents.Any() && !string.IsNullOrEmpty(context);
            var confidence = documents.Count >= 2 ? "high" : documents.Count == 1 ? "medium" : "low";

            FAQAgentResponse response;

            if (hasRelevantInfo)
            {
                // Use the AI agent to generate a comprehensive answer based on the documents
                var prompt = $"Question: {query}\n\nRelevant Documents:\n{context}\n\nPlease provide a comprehensive answer based on the provided documents.";

                string lastResponse;

                if (streamingChannel != null)
                {
                    Logger.LogInformation("STREAMING ENABLED: Using real-time streaming for conversation thread {ThreadId}", ThreadId);

                    // Stream the response in real-time using Agent Framework
                    await streamingChannel.WriteAsync("status", "Generating answer using AI...");

                    var responseBuilder = new StringBuilder();

                    await foreach (var update in RunStreamingAsync(prompt))
                    {
                        var chunk = update.Text ?? "";
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            responseBuilder.Append(chunk);
                            // Stream each chunk to the client immediately
                            Logger.LogInformation("STREAMING CHUNK: {Chunk}", chunk);
                            await streamingChannel.WriteAsync("chat", chunk);
                        }
                    }

                    lastResponse = responseBuilder.ToString();
                    Logger.LogInformation("STREAMING COMPLETE: Total response length {Length}", lastResponse.Length);
                }
                else
                {
                    Logger.LogWarning("STREAMING DISABLED: streamingChannel is null, falling back to non-streaming");

                    // Fallback to non-streaming for backward compatibility
                    var agentResponse = await RunAsync(prompt);
                    lastResponse = agentResponse.Text ?? "";
                }

                response = new FAQAgentResponse
                {
                    Answer = !string.IsNullOrEmpty(lastResponse) ? lastResponse : "I was unable to generate a response based on the provided documents.",
                    ConfidenceLevel = confidence,
                    HasCompleteAnswer = hasRelevantInfo,
                    SourceDocuments = documents.Select(d => d.Title).ToList(),
                    Citations = new List<DocumentCitation>(),
                    FollowUpSuggestions = hasRelevantInfo ? [
                        "What are the specific requirements for partnership compliance?",
                        "How are revenue calculations performed?",
                        "What are the termination procedures for partnerships?"
                    ] : []
                };
            }
            else
            {
                response = new FAQAgentResponse
                {
                    Answer = "I don't have enough information in the available documents to answer your question.",
                    ConfidenceLevel = "low",
                    HasCompleteAnswer = false,
                    SourceDocuments = new List<string>(),
                    Citations = new List<DocumentCitation>(),
                    FollowUpSuggestions = new List<string>()
                };
            }

            // Extract citations for the answer if we have content
            if (hasRelevantInfo && !string.IsNullOrEmpty(response.Answer))
            {
                response.Citations = await _citationService.ExtractCitationsAsync(query, response.Answer, documents);
            }

            Logger.LogInformation("Generated structured response with confidence: {Confidence} and {CitationCount} citations",
                response.ConfidenceLevel, response.Citations?.Count ?? 0);
            return response;
        }
        catch (Exception ex) when (Log(ex, $"Error generating answer for session {ThreadId} with query {query}"))
        {
            return new FAQAgentResponse
            {
                Answer = "I encountered an error while processing your request. Please try again.",
                ConfidenceLevel = "low",
                HasCompleteAnswer = false
            };
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public async Task<FAQAgentResponse> GenerateStructuredResponseAsync(string query, List<DocumentResult> relevantDocuments, IBidirectionalToClientChannel? streamingChannel = null)
    {
        // Debug logging to trace streaming channel parameter
        Logger.LogInformation("ResponseGenerationAgent.GenerateStructuredResponseAsync called with streamingChannel (not null: {NotNull}, type: {Type}) for thread {ThreadId}",
            streamingChannel != null, streamingChannel?.GetType().Name, ThreadId);

        // Pass the streaming channel to enable real-time response streaming
        return await GenerateAnswer(query, relevantDocuments, streamingChannel);
    }

    /// <summary>
    /// Legacy method for backward compatibility - returns just the answer text.
    /// </summary>
    public async Task<string> GenerateResponseAsync(string query, List<DocumentResult> relevantDocuments)
    {
        var structuredResponse = await GenerateStructuredResponseAsync(query, relevantDocuments);
        return structuredResponse.Answer;
    }
}
