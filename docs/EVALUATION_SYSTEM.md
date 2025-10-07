# Partnership Agent Evaluation System

## Overview

The Partnership Agent includes a comprehensive AI response evaluation system built on Microsoft's Extensions.AI.Evaluation framework. This system automatically assesses the quality of AI-generated responses using multiple metrics and can optionally compare responses against ground truth data.

## Architecture

### Core Components

The evaluation system consists of several key components:

- **`IAssistantResponseEvaluator`** (`src/PartnershipAgent.Core/Evaluation/IAssistantResponseEvaluator.cs:13`) - Main evaluation interface
- **`AssistantResponseEvaluator`** (`src/PartnershipAgent.Core/Evaluation/AssistantResponseEvaluator.cs:16`) - Concrete implementation using Microsoft.Extensions.AI.Evaluation
- **`IGroundTruthService`** (`src/PartnershipAgent.Core/Services/IGroundTruthService.cs`) - Interface for managing ground truth data
- **`GroundTruthService`** (`src/PartnershipAgent.Core/Services/GroundTruthService.cs`) - Implementation that loads ground truth from CSV resources

### Integration

Evaluations are integrated into the response pipeline at `src/PartnershipAgent.Core/Steps/ResponseGenerationStep.cs` and run asynchronously as fire-and-forget operations to avoid impacting response times. All evaluation metrics are automatically logged to OpenTelemetry for monitoring and analysis.

## Microsoft.Extensions.AI.Evaluation Package

The system uses Microsoft's official evaluation libraries:

- **Microsoft.Extensions.AI.Evaluation** (v9.4.0-preview.1.25207.5) - Core abstractions and types
- **Microsoft.Extensions.AI.Evaluation.Quality** (v9.4.0-preview.1.25207.5) - Quality evaluators
- **Microsoft.Extensions.AI.Evaluation.Safety** - Safety and bias evaluators (available but not currently used)

### Package Documentation

For comprehensive documentation on the Microsoft.Extensions.AI.Evaluation framework:

- [Official Documentation](https://learn.microsoft.com/en-us/dotnet/ai/conceptual/evaluation-libraries)
- [Quality Evaluators Blog Post](https://devblogs.microsoft.com/dotnet/exploring-agent-quality-and-nlp-evaluators/)
- [Evaluation SDK Guide](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/evaluate-sdk)
- [NuGet Package](https://www.nuget.org/packages/Microsoft.Extensions.AI.Evaluation.Quality)

## Evaluation Types

### Core Quality Evaluators (Always Applied)

These evaluators run on every response regardless of whether ground truth is available:

#### **Coherence Evaluator**
- **Purpose**: Measures the logical flow, consistency, and overall structure of the response
- **Scoring**: Typically 1-5 scale where higher scores indicate better logical coherence
- **Use Case**: Ensures responses are well-structured and internally consistent

#### **Fluency Evaluator**
- **Purpose**: Assesses language quality, grammar, readability, and natural language flow
- **Scoring**: 1-5 scale where higher scores indicate better language quality
- **Use Case**: Ensures responses are well-written and easy to understand

### Ground Truth-Based Evaluators (Applied When Expected Answers Available)

These evaluators compare responses against known correct answers:

#### **Equivalence Evaluator**
- **Purpose**: Measures semantic similarity between the generated response and expected answer
- **Method**: Uses natural language understanding to compare meaning rather than exact text matching
- **Scoring**: Typically 0-1 scale where 1 indicates semantic equivalence
- **Use Case**: Determines if the response conveys the same information as the expected answer

#### **Groundedness Evaluator**
- **Purpose**: Verifies that the response is factually accurate and supported by the ground truth
- **Method**: Analyzes whether claims in the response are supported by the reference information
- **Scoring**: 0-1 scale where 1 indicates full groundedness
- **Use Case**: Ensures responses don't contain hallucinations or unsupported claims

### Additional Available Evaluators

The Microsoft.Extensions.AI.Evaluation.Quality package includes additional evaluators that can be integrated:

#### **Quality Evaluators**
- **Relevance Evaluator** - Measures how well the response addresses the user's question
- **Truth Evaluator** - Assesses factual accuracy of the response
- **Completeness Evaluator** - Evaluates whether the response fully answers the question
- **Retrieval Evaluator** - Measures effectiveness of information retrieval in RAG scenarios

#### **Agent-Specific Evaluators**
- **IntentResolution Evaluator** - Measures how effectively an agent understands user intent
- **TaskAdherence Evaluator** - Evaluates whether an agent stays focused on assigned tasks
- **ToolCallAccuracy Evaluator** - Assesses accuracy of tool calls made by agents

#### **Safety Evaluators** (Available in separate package)
- **Content Safety Evaluators** - Detect harmful, offensive, or inappropriate content
- **Bias Detection Evaluators** - Identify potential biases in responses
- **Toxicity Evaluators** - Measure toxic language or content

## Ground Truth System

### Data Structure

Ground truth data is stored in `src/PartnershipAgent.Core/Data/GroundTruthData.csv` with the following schema:

```csv
UserPrompt,ExpectedOutput,Category,Module
```

- **UserPrompt**: The user's input question or prompt
- **ExpectedOutput**: The ideal response for comparison
- **Category**: Classification (e.g., General, Financial, Legal, etc.)
- **Module**: Target module (FAQAgent, EntityResolutionAgent, etc.)

### Matching Logic

The ground truth service (`src/PartnershipAgent.Core/Services/GroundTruthService.cs`) supports:

- **Exact matching**: Case-insensitive exact prompt matching
- **Partial matching**: Substring matching for flexible prompt variations
- **Module-specific filtering**: Retrieves ground truth specific to the current module

### Current Ground Truth Coverage

The system includes 16 sample ground truth entries covering:

- **Partnership FAQ** (11 entries): General partnership questions, financial arrangements, exit procedures, governance, tax implications, etc.
- **Entity Extraction** (5 entries): Entity recognition and extraction scenarios

## Configuration

### Enabling/Disabling Evaluation

Evaluation can be controlled via configuration:

```json
{
  "Evaluation": {
    "Enabled": true
  }
}
```

### Dependency Injection Setup

The system is configured in `src/PartnershipAgent.WebApi/Program.cs`:

```csharp
// Conditional registration based on configuration
if (evaluationEnabled)
{
    builder.Services.AddScoped<IAssistantResponseEvaluator, AssistantResponseEvaluator>();
    builder.Services.AddSingleton<IGroundTruthService, GroundTruthService>();
}
```

## Observability and Monitoring

### OpenTelemetry Integration

All evaluation metrics are automatically logged to OpenTelemetry with the activity source "PartnershipAgent.Evaluation". Metrics include:

- Individual evaluator scores (e.g., `gen_ai.evaluation.Coherence.score`)
- Evaluation context (`gen_ai.evaluation.module`, `gen_ai.evaluation.user_prompt`)
- Ground truth availability flag (`gen_ai.evaluation.has_ground_truth`)
- Full response content (`gen_ai.full_nl_response`)

### Metric Analysis

Evaluation data can be analyzed through:

- **Azure Monitor** - For production monitoring and alerting
- **Console Export** - For development and debugging
- **Custom Exporters** - For integration with other monitoring systems

## Extending the Evaluation System

### Adding New Quality Evaluators

To add additional Microsoft evaluators:

1. **Update Package References** (if needed):
   ```xml
   <PackageReference Include="Microsoft.Extensions.AI.Evaluation.Safety" Version="9.4.0-preview.1.25207.5" />
   ```

2. **Modify AssistantResponseEvaluator.cs**:
   ```csharp
   // Add to the evaluators list at line 57-61
   var evaluators = new List<IEvaluator>
   {
       new CoherenceEvaluator(),
       new FluencyEvaluator(),
       new RelevanceEvaluator(),        // New evaluator
       new CompletenessEvaluator()      // New evaluator
   };
   ```

3. **Update OpenTelemetry Logging** (if needed):
   Add any new metric names to the logging section at lines 107-113.

### Creating Custom Evaluators

Implement the `IEvaluator` interface for domain-specific evaluation:

```csharp
public class CustomPartnershipEvaluator : IEvaluator
{
    public string Name => "PartnershipSpecific";
    
    public async Task<EvaluationResult> EvaluateAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatResponse response,
        ChatConfiguration chatConfiguration,
        IReadOnlyList<EvaluationContext>? contexts = null,
        CancellationToken cancellationToken = default)
    {
        // Custom evaluation logic here
        // Return EvaluationResult with custom metrics
    }
}
```

### Extending Ground Truth Data

1. **Add New Entries**: Extend `GroundTruthData.csv` with new UserPrompt/ExpectedOutput pairs
2. **New Categories**: Add domain-specific categories for better organization
3. **Multiple Modules**: Support additional modules beyond FAQAgent and EntityResolutionAgent
4. **Dynamic Loading**: Consider database storage for larger ground truth datasets

### Advanced Customization

#### Custom Evaluation Contexts

Create specialized contexts for evaluators that need additional information:

```csharp
public class PartnershipEvaluationContext : EvaluationContext
{
    public string PartnershipType { get; set; }
    public string LegalJurisdiction { get; set; }
    // Additional domain-specific context
}
```

#### Multi-Model Evaluation

Configure different models for evaluation vs. generation:

```csharp
// Use a different model specifically for evaluation
var evaluationChatClient = kernel.GetRequiredService<IChatClient>("evaluation-model");
var chatConfiguration = new ChatConfiguration(evaluationChatClient);
```

#### Batch Evaluation

For processing multiple responses:

```csharp
var batchEvaluator = new BatchEvaluator(evaluators);
var results = await batchEvaluator.EvaluateAsync(messagesBatch, responsesBatch, chatConfiguration);
```

## Best Practices

### Performance Considerations

- **Asynchronous Execution**: Evaluations run as fire-and-forget to avoid blocking response generation
- **Selective Evaluation**: Consider enabling evaluation only in development/staging for cost optimization
- **Caching**: Cache ground truth data in memory for faster lookups

### Quality Assurance

- **Regular Ground Truth Updates**: Keep ground truth data current with business requirements
- **Threshold Monitoring**: Set up alerts for evaluation scores below acceptable thresholds
- **A/B Testing**: Use evaluation metrics to compare different model configurations

### Cost Management

- **Evaluation Model Selection**: Consider using smaller, cost-effective models specifically for evaluation
- **Sampling**: Evaluate a subset of responses in high-volume scenarios
- **Batch Processing**: Process evaluations in batches during off-peak hours

## Troubleshooting

### Common Issues

1. **Missing Ground Truth**: Ensure CSV file is marked as `EmbeddedResource` in the project file
2. **Evaluation Failures**: Check OpenTelemetry logs for detailed error information
3. **Performance Impact**: Verify evaluations are running asynchronously
4. **Configuration Issues**: Confirm `Evaluation:Enabled` setting and dependency injection setup

### Debugging

Enable console logging to see evaluation results during development:

```csharp
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.SetMinimumLevel(LogLevel.Information);
});
```

## Future Enhancements

### Potential Improvements

- **Real-time Evaluation Dashboard**: Web interface for monitoring evaluation metrics
- **Automated Ground Truth Generation**: Use high-quality models to generate ground truth data
- **Comparative Evaluation**: Compare responses from different models or configurations
- **Domain-Specific Evaluators**: Partnership law and business-specific evaluation criteria
- **User Feedback Integration**: Incorporate human feedback into evaluation scores
- **Continuous Learning**: Update ground truth based on successful response patterns

### Research Opportunities

- **Custom Partnership Metrics**: Develop evaluators specific to partnership law and business contexts
- **Multi-turn Conversation Evaluation**: Assess quality across entire conversation flows
- **Context-Aware Evaluation**: Consider conversation history and user context in evaluation
- **Explainable Evaluation**: Provide detailed explanations for evaluation scores