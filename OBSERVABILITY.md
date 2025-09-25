# Partnership Agent Observability

This document describes the observability features implemented in the Partnership Agent solution, including OpenTelemetry integration and AI response evaluation metrics.

## Overview

The Partnership Agent now includes comprehensive observability features that automatically capture and send evaluation metrics to Application Insights via OpenTelemetry. This enables continuous monitoring of AI response quality across all user interactions.

## Features

### AI Response Evaluation
- **Coherence**: Measures how well the response flows logically and maintains internal consistency
- **Fluency**: Evaluates the grammatical correctness and natural language quality
- **Equivalence**: (When ground truth provided) Measures semantic similarity to expected answers
- **Groundedness**: (When ground truth provided) Evaluates factual accuracy against reference material

### Telemetry Integration
- OpenTelemetry integration with Application Insights
- Automatic activity tracing for all AI interactions
- Custom metrics for evaluation scores
- Structured logging with correlation IDs

## Configuration

### Application Insights Connection String
Set your Application Insights connection string in one of the following ways:

#### Option 1: appsettings.json
```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/"
  }
}
```

#### Option 2: Environment Variable
```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/"
```

### Evaluation Configuration
Enable or disable AI response evaluation:

```json
{
  "Evaluation": {
    "Enabled": true,
    "LogToConsole": true
  }
}
```

## Usage

### Viewing Metrics in Application Insights

1. **Navigate to Application Insights** in the Azure portal
2. **Go to Logs** and run KQL queries to analyze AI quality metrics:

```kql
// View all evaluation metrics
traces
| where customDimensions contains "gen_ai.evaluation"
| project timestamp, operation_Id, customDimensions
| sort by timestamp desc

// Average coherence scores by time
traces
| where customDimensions has "gen_ai.evaluation.Coherence.score"
| extend CoherenceScore = todouble(customDimensions["gen_ai.evaluation.Coherence.score"])
| summarize AvgCoherence = avg(CoherenceScore) by bin(timestamp, 1h)
| sort by timestamp desc

// Response quality distribution
traces
| where customDimensions has "gen_ai.evaluation.Fluency.score"
| extend FluencyScore = todouble(customDimensions["gen_ai.evaluation.Fluency.score"])
| summarize count() by bin(FluencyScore, 0.1)
| sort by FluencyScore asc
```

3. **Create dashboards** to monitor:
   - Average evaluation scores over time
   - Response quality distribution
   - Failed evaluations and error rates
   - User interaction patterns

### Custom Metrics Available

Each AI interaction generates the following telemetry tags:

- `gen_ai.full_nl_response`: The complete AI response text
- `gen_ai.evaluation.module`: Which AI component generated the response (e.g., "FAQAgent", "EntityResolutionAgent")
- `gen_ai.evaluation.user_prompt`: The original user input
- `gen_ai.evaluation.Coherence.score`: Coherence evaluation score (0.0-1.0)
- `gen_ai.evaluation.Fluency.score`: Fluency evaluation score (0.0-1.0)
- `gen_ai.evaluation.has_ground_truth`: Whether ground truth comparison was performed

When ground truth is available:
- `gen_ai.evaluation.Equivalence.score`: Semantic similarity score
- `gen_ai.evaluation.Groundedness.score`: Factual accuracy score

## Architecture

### Components

1. **IAssistantResponseEvaluator**: Interface for AI response evaluation
2. **AssistantResponseEvaluator**: Implementation using Microsoft.Extensions.AI.Evaluation
3. **OpenTelemetry Configuration**: Automatic tracing and metrics collection
4. **Activity Sources**: 
   - `PartnershipAgent.StepOrchestration`: Main orchestration flow
   - `PartnershipAgent.Agents`: Individual agent activities
   - `PartnershipAgent.Evaluation`: Evaluation activities

### Integration Points

- **StepOrchestrationService**: Evaluates complete conversation quality
- **ResponseGenerationStep**: Evaluates FAQ agent responses
- **EntityResolutionStep**: Evaluates entity extraction quality

## Best Practices

### Monitoring
- Set up alerts for low evaluation scores (< 0.7)
- Monitor evaluation failure rates
- Track response time impact of evaluation

### Performance
- Evaluation runs asynchronously and doesn't block user responses
- Failed evaluations are logged but don't affect user experience
- Consider disabling evaluation in high-throughput scenarios

### Ground Truth Evaluation
- Provide expected answers when available for more comprehensive metrics
- Use integration tests with known good responses
- Implement feedback loops to improve model performance

## Troubleshooting

### Common Issues

**Evaluation metrics not appearing in Application Insights:**
1. Verify connection string is set correctly
2. Check that `Evaluation:Enabled` is set to `true`
3. Ensure the Microsoft.Extensions.AI.Evaluation package is compatible

**High latency:**
1. Monitor evaluation execution time in logs
2. Consider disabling evaluation for high-traffic endpoints
3. Use sampling to reduce evaluation frequency

**Missing ground truth metrics:**
- Equivalence and Groundedness evaluators only run when expectedAnswer is provided
- Check that ground truth data is being passed to evaluation methods

## Sample Queries

### Monitor AI Quality Trends
```kql
traces
| where customDimensions has "gen_ai.evaluation"
| extend 
    Coherence = todouble(customDimensions["gen_ai.evaluation.Coherence.score"]),
    Fluency = todouble(customDimensions["gen_ai.evaluation.Fluency.score"]),
    Module = tostring(customDimensions["gen_ai.evaluation.module"])
| where isnotnull(Coherence) and isnotnull(Fluency)
| summarize 
    AvgCoherence = avg(Coherence),
    AvgFluency = avg(Fluency),
    Count = count()
    by Module, bin(timestamp, 1h)
| sort by timestamp desc
```

### Identify Low-Quality Responses
```kql
traces
| where customDimensions has "gen_ai.evaluation.Coherence.score"
| extend 
    Coherence = todouble(customDimensions["gen_ai.evaluation.Coherence.score"]),
    Response = tostring(customDimensions["gen_ai.full_nl_response"]),
    UserPrompt = tostring(customDimensions["gen_ai.evaluation.user_prompt"])
| where Coherence < 0.7
| project timestamp, UserPrompt, Response, Coherence
| sort by timestamp desc
```