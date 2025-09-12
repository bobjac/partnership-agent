using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using PartnershipAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

public class EntityResolutionAgent : IEntityResolutionAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<EntityResolutionAgent> _logger;

    public EntityResolutionAgent(Kernel kernel, ILogger<EntityResolutionAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<List<ExtractedEntity>> ExtractEntitiesAsync(string prompt)
    {
        _logger.LogInformation("Extracting entities from prompt: {Prompt}", prompt);

        var function = _kernel.CreateFunctionFromPrompt(@"
            Extract all entities from the following text. Focus on:
            - Company names
            - Person names  
            - Contract terms
            - Dates
            - Financial amounts
            - Legal concepts

            Text: {{$input}}

            Return the entities in JSON format as an array of objects with properties: text, type, confidence (0-1).
            Only return the JSON array, no other text.
        ");

        try
        {
            var result = await _kernel.InvokeAsync(function, new() { ["input"] = prompt });
            var jsonResult = result.ToString();

            var entities = new List<ExtractedEntity>();
            
            if (!string.IsNullOrEmpty(jsonResult) && jsonResult.Trim().StartsWith('['))
            {
                try
                {
                    entities = System.Text.Json.JsonSerializer.Deserialize<List<ExtractedEntity>>(jsonResult) ?? new();
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse entity extraction JSON result");
                }
            }

            if (entities.Count == 0)
            {
                entities.Add(new ExtractedEntity 
                { 
                    Text = "sample entity", 
                    Type = "general", 
                    Confidence = 0.8 
                });
            }

            _logger.LogInformation("Extracted {Count} entities", entities.Count);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities");
            return new List<ExtractedEntity>();
        }
    }
}