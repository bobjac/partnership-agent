using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PartnershipAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

/// <summary>
/// The EntityResolutionAgent is a specialized agent that extracts entities from user input
/// using structured responses and LLM-driven analysis.
/// </summary>
public class EntityResolutionAgent : BaseChatHistoryAgent
{
    private readonly IKernelBuilder _kernelBuilder;

    /// <summary>
    /// The name of the agent used for identification in the semantic kernel system.
    /// </summary>
    public override string Name => "EntityResolutionAgent";

    /// <summary>
    /// Brief description of the agent's purpose for documentation and display.
    /// </summary>
    public override string Description => "Agent that extracts and analyzes entities from user input using structured responses";

    /// <summary>
    /// Constructor for the EntityResolutionAgent.
    /// Initializes the agent with required dependencies and configures the semantic kernel.
    /// </summary>
    public EntityResolutionAgent(
        Guid threadId,
        IKernelBuilder kernelBuilder,
        IRequestedBy requestedBy,
        ILogger<EntityResolutionAgent> logger
    ) : base(requestedBy, threadId, logger)
    {
        _kernelBuilder = kernelBuilder ?? throw new ArgumentNullException(nameof(kernelBuilder));
        InitializeAgent();
    }

    /// <summary>
    /// Initializes the ChatCompletionAgent with the appropriate settings and instructions.
    /// </summary>
    private void InitializeAgent()
    {
        Kernel kernel = _kernelBuilder.Build();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            ResponseFormat = typeof(EntityResolutionResponse)
        };

        var arguments = new KernelArguments(executionSettings);
        var instructions = @"
            You are a helpful assistant that extracts entities from user text about partnership agreements and business documents.
            Use the ExtractEntities function to analyze the user's input and extract meaningful entities.
            
            Always respond with:
            - The original prompt that was analyzed
            - List of extracted entities with their types and confidence scores
            - Your confidence level (high/medium/low) in the extraction quality
            - Whether meaningful entities were found
            - A brief summary of the types of entities discovered
            - Suggestions for improving the query if needed
            
            Focus on extracting:
            - Company names and organizations
            - Person names and roles
            - Contract terms and legal concepts
            - Dates and time periods
            - Financial amounts and percentages
            - Partnership-related terminology
            - Business metrics and KPIs
            
            Provide confidence scores from 0.0 to 1.0 for each entity based on how certain you are about the extraction.
        ";

        Agent = new ChatCompletionAgent
        {
            Name = Name,
            Description = Description,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = arguments
        };

        Agent.Kernel.Plugins.AddFromObject(this);
    }

    /// <summary>
    /// Extracts entities from the provided text using LLM analysis.
    /// This method is exposed as a KernelFunction for the agent to use.
    /// </summary>
    /// <param name="prompt">The text to analyze for entity extraction</param>
    /// <returns>List of extracted entities with metadata</returns>
    [KernelFunction, Description("Extracts entities from text focusing on partnership and business-related terms.")]
    public async Task<List<ExtractedEntity>> ExtractEntities(
        [Description("The text to analyze for entity extraction")] string prompt)
    {
        Logger.LogInformation("Extracting entities from prompt: {Prompt}", prompt);

        try
        {
            var entities = new List<ExtractedEntity>();

            // Simple entity extraction logic - in a real implementation, this could use NLP libraries
            var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for partnership-related terms
            var partnershipTerms = new[] { "partner", "partnership", "revenue", "tier", "percentage", "sharing", "agreement", "contract" };
            var financialTerms = new[] { "%", "percent", "dollar", "$", "cost", "fee", "payment" };
            var companyIndicators = new[] { "Inc", "LLC", "Corp", "Company", "Ltd" };

            foreach (var word in words)
            {
                var cleanWord = word.Trim('.', ',', '?', '!', ';', ':').ToLowerInvariant();
                
                if (partnershipTerms.Contains(cleanWord))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Text = word.Trim('.', ',', '?', '!', ';', ':'),
                        Type = "partnership_term",
                        Confidence = 0.9
                    });
                }
                else if (financialTerms.Any(t => cleanWord.Contains(t)))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Text = word.Trim('.', ',', '?', '!', ';', ':'),
                        Type = "financial",
                        Confidence = 0.8
                    });
                }
                else if (companyIndicators.Any(c => word.Contains(c)))
                {
                    entities.Add(new ExtractedEntity
                    {
                        Text = word.Trim('.', ',', '?', '!', ';', ':'),
                        Type = "company",
                        Confidence = 0.7
                    });
                }
            }

            // Remove duplicates and ensure we have at least some entities
            entities = entities.GroupBy(e => e.Text.ToLowerInvariant())
                             .Select(g => g.First())
                             .ToList();

            if (entities.Count == 0)
            {
                entities.Add(new ExtractedEntity 
                { 
                    Text = "general inquiry", 
                    Type = "general", 
                    Confidence = 0.6 
                });
            }

            Logger.LogInformation("Extracted {Count} entities", entities.Count);
            return entities;
        }
        catch (Exception ex) when (LogException(ex, $"Error extracting entities from prompt: {prompt}"))
        {
            return new List<ExtractedEntity>
            {
                new ExtractedEntity 
                { 
                    Text = "error", 
                    Type = "general", 
                    Confidence = 0.1 
                }
            };
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// </summary>
    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(string prompt)
    {
        return await ExtractEntities(prompt);
    }

    /// <summary>
    /// Helper method for logging exceptions with consistent format.
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">Additional context message</param>
    /// <returns>Always returns true for use in when clauses</returns>
    private bool LogException(Exception ex, string message)
    {
        Logger.LogError(ex, "EntityResolutionAgent error: {Message}", message);
        return true;
    }
}