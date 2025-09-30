# Monitoring and Telemetry

This document provides comprehensive guidance for monitoring and analyzing the Partnership Agent system using Azure Application Insights, including KQL queries for evaluation metrics, performance analysis, and troubleshooting.

## Table Structure

The Partnership Agent telemetry data is stored in two main tables:
- **`dependencies`** - External service calls (Azure OpenAI, Azure Search)
- **`requests`** - HTTP API requests to the Partnership Agent

## Evaluation Queries

### 1. Check for Evaluation Data in Dependencies

```kql
// Look for evaluation activities in dependencies
dependencies
| where timestamp >= ago(24h)
| where cloud_RoleName == "PartnershipAgent"
| where customDimensions has "gen_ai.evaluation" or 
        customDimensions has "evaluation" or
        name contains "evaluation"
| extend 
    Module = tostring(customDimensions["gen_ai.evaluation.module"]),
    UserPrompt = tostring(customDimensions["gen_ai.evaluation.user_prompt"]),
    HasGroundTruth = tobool(customDimensions["gen_ai.evaluation.has_ground_truth"]),
    CoherenceScore = toreal(customDimensions["gen_ai.evaluation.coherence.score"]),
    FluencyScore = toreal(customDimensions["gen_ai.evaluation.fluency.score"]),
    EquivalenceScore = toreal(customDimensions["gen_ai.evaluation.equivalence.score"]),
    GroundednessScore = toreal(customDimensions["gen_ai.evaluation.groundedness.score"])
| project 
    timestamp,
    name,
    Module,
    UserPrompt,
    HasGroundTruth,
    CoherenceScore,
    FluencyScore,
    EquivalenceScore,
    GroundednessScore,
    customDimensions
| order by timestamp desc
```

### 2. Check for Evaluation Data in Requests

```kql
// Look for evaluation data in requests table
requests
| where timestamp >= ago(24h)
| where cloud_RoleName == "PartnershipAgent"
| where customDimensions has "gen_ai.evaluation" or url contains "chat"
| extend 
    Module = tostring(customDimensions["gen_ai.evaluation.module"]),
    UserPrompt = tostring(customDimensions["gen_ai.evaluation.user_prompt"]),
    HasGroundTruth = tobool(customDimensions["gen_ai.evaluation.has_ground_truth"]),
    CoherenceScore = toreal(customDimensions["gen_ai.evaluation.coherence.score"]),
    FluencyScore = toreal(customDimensions["gen_ai.evaluation.fluency.score"]),
    EquivalenceScore = toreal(customDimensions["gen_ai.evaluation.equivalence.score"]),
    GroundednessScore = toreal(customDimensions["gen_ai.evaluation.groundedness.score"])
| project 
    timestamp,
    name,
    url,
    Module,
    UserPrompt,
    HasGroundTruth,
    CoherenceScore,
    FluencyScore,
    EquivalenceScore,
    GroundednessScore,
    duration,
    resultCode
| order by timestamp desc
```

### 3. Evaluation Summary by Module

```kql
// Summary of evaluation scores by module
union dependencies, requests
| where timestamp >= ago(24h)
| where cloud_RoleName == "PartnershipAgent"
| where customDimensions has "gen_ai.evaluation"
| extend 
    Module = tostring(customDimensions["gen_ai.evaluation.module"]),
    HasGroundTruth = tobool(customDimensions["gen_ai.evaluation.has_ground_truth"]),
    CoherenceScore = toreal(customDimensions["gen_ai.evaluation.coherence.score"]),
    FluencyScore = toreal(customDimensions["gen_ai.evaluation.fluency.score"]),
    EquivalenceScore = toreal(customDimensions["gen_ai.evaluation.equivalence.score"]),
    GroundednessScore = toreal(customDimensions["gen_ai.evaluation.groundedness.score"])
| where isnotnull(CoherenceScore)
| summarize 
    TotalEvaluations = count(),
    GroundTruthEvaluations = countif(HasGroundTruth == true),
    AvgCoherence = round(avg(CoherenceScore), 3),
    AvgFluency = round(avg(FluencyScore), 3),
    AvgEquivalence = round(avg(EquivalenceScore), 3),
    AvgGroundedness = round(avg(GroundednessScore), 3)
    by Module
| order by TotalEvaluations desc
```

## Performance Queries

### 4. OpenAI API Performance Analysis

```kql
// Analyze OpenAI API calls and performance
dependencies
| where timestamp >= ago(24h)
| where target contains "cognitiveservices.azure.com"
| extend 
    Model = extract(@"deployments/([^/]+)", 1, data),
    ThreadId = tostring(customDimensions.ThreadId)
| summarize 
    CallCount = count(),
    AvgDuration = avg(duration),
    MaxDuration = max(duration),
    MinDuration = min(duration),
    SuccessRate = round(100.0 * countif(success == true) / count(), 1)
    by Model, resultCode
| order by Model, AvgDuration desc
```

### 5. Chat API Request Summary

```kql
// Analyze chat API requests
requests
| where timestamp >= ago(24h)
| where url contains "chat" and cloud_RoleName == "PartnershipAgent"
| extend ThreadId = tostring(customDimensions.ThreadId)
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    MaxDuration = max(duration),
    SuccessRate = round(100.0 * countif(resultCode < 400) / count(), 1)
    by bin(timestamp, 1h)
| render timechart
```

### 6. Streaming Performance Analysis

```kql
// Analyze streaming response performance
requests
| where timestamp >= ago(24h)
| where url contains "stream" and cloud_RoleName == "PartnershipAgent"
| extend ThreadId = tostring(customDimensions.ThreadId)
| project 
    timestamp,
    ThreadId,
    duration,
    resultCode,
    url
| order by timestamp desc
```

## Diagnostic Queries

### 7. Error Analysis

```kql
// Analyze errors and failures
union dependencies, requests
| where timestamp >= ago(24h)
| where cloud_RoleName == "PartnershipAgent"
| where success == false or resultCode >= 400
| project 
    timestamp,
    $table,
    name,
    resultCode,
    duration,
    customDimensions
| order by timestamp desc
```

### 8. Data Availability Check

```kql
// Check what Partnership Agent data exists
union dependencies, requests
| where timestamp >= ago(24h)
| where cloud_RoleName == "PartnershipAgent"
| extend TableName = $table
| summarize Count = count() by TableName, bin(timestamp, 1h)
| render timechart
```

## Evaluation Score Interpretation

**Score Ranges (0.0 - 1.0):**
- **Coherence**: Logical flow and consistency of the response
- **Fluency**: Language quality and readability  
- **Equivalence**: Semantic similarity to ground truth (only available with ground truth)
- **Groundedness**: Factual accuracy against ground truth (only available with ground truth)

**Score Quality Levels:**
- **> 0.8**: Excellent
- **0.6 - 0.8**: Good  
- **< 0.6**: Needs improvement

## Configuration Notes

- Evaluation is controlled by `"Evaluation:Enabled": true` in appsettings.json
- Ground truth data is stored in `/src/PartnershipAgent.Core/Data/GroundTruthData.csv`
- Telemetry data may take 2-5 minutes to appear in Application Insights
- The system uses OpenTelemetry to send data to Azure Monitor

## Troubleshooting

If queries return no data:
1. Wait 2-5 minutes for telemetry to populate
2. Check if evaluation is enabled in configuration
3. Verify the correct Application Insights resource is being used
4. Run a test query to generate new evaluation data