# Migration Guide: Semantic Kernel to Microsoft Agent Framework

## Overview

This guide documents the migration from **Semantic Kernel v1.48.0** to **Microsoft Agent Framework v1.0.0-preview.251016.1** (also known as Microsoft.Agents.AI). This migration was completed for the Partnership Agent project and provides a comprehensive reference for similar migrations.

## Table of Contents

1. [Why Migrate?](#why-migrate)
2. [Key Architectural Differences](#key-architectural-differences)
3. [Migration Strategy](#migration-strategy)
4. [Step-by-Step Migration Guide](#step-by-step-migration-guide)
5. [Code Examples](#code-examples)
6. [Common Patterns](#common-patterns)
7. [Structured Output Implementation](#structured-output-implementation)
8. [Testing Strategy](#testing-strategy)
9. [Troubleshooting](#troubleshooting)
10. [Lessons Learned](#lessons-learned)

---

## Why Migrate?

The Microsoft Agent Framework represents the next generation of AI agent development from Microsoft, offering:

- **Better abstractions**: Clean separation between agent logic and LLM implementation
- **Improved composition**: Easier to build multi-agent workflows
- **Standardized patterns**: Consistent patterns for agent communication and orchestration
- **Framework agnostic**: Works with any LLM provider through `IChatClient`
- **Production-ready**: Built on lessons learned from Semantic Kernel
- **Future-proof**: Microsoft's recommended path forward for agent development

## Key Architectural Differences

### Type System Changes

| Semantic Kernel | Agent Framework | Purpose |
|----------------|-----------------|---------|
| `ChatMessageContent` | `ChatMessage` | Individual chat messages |
| `ChatHistory` | `IList<ChatMessage>` | Message collections |
| `AuthorRole` | `ChatRole` | Message sender role |
| `Kernel` | `IChatClient` | LLM abstraction |
| `KernelArguments` | `ChatOptions` | Request configuration |
| `IChatCompletionService` | `IChatClient` | Chat completion interface |

### Agent Implementation

**Semantic Kernel:**
```csharp
public class MyAgent : ChatCompletionAgent
{
    public MyAgent(Kernel kernel, string instructions)
    {
        Kernel = kernel;
        Instructions = instructions;
    }
}
```

**Agent Framework:**
```csharp
public class MyAgent : BaseAgent
{
    private readonly IChatClient _chatClient;

    public MyAgent(IChatClient chatClient, ILogger logger)
    {
        _chatClient = chatClient;
        Agent = new ChatClientAgent(_chatClient, new ChatClientAgentOptions
        {
            Name = "MyAgent",
            Instructions = "Your instructions here"
        });
    }
}
```

### Chat History Management

**Semantic Kernel:**
```csharp
public interface IChatHistoryService
{
    Task AddMessageToChatHistoryAsync(Guid threadId, ChatMessageContent message);
    Task<ChatHistory> GetChatHistoryAsync(Guid threadId);
}
```

**Agent Framework:**
```csharp
public interface IChatHistoryService
{
    Task AddMessageToChatHistoryAsync(Guid threadId, ChatMessage message);
    Task<IList<ChatMessage>> GetChatHistoryAsync(Guid threadId);
}
```

---

## Migration Strategy

### Phase-Based Approach

We used a **coexistence strategy** where both systems ran side-by-side during migration:

1. **Phase 1**: Create V2 agents alongside V1 agents
2. **Phase 2**: Migrate supporting services (chat history, citations)
3. **Phase 3**: Test V2 agents thoroughly
4. **Phase 4**: Add structured output support
5. **Phase 5**: Remove V1 dependencies

### Benefits of This Approach

- **Low risk**: Can rollback at any point
- **Incremental testing**: Test each agent independently
- **Easy comparison**: Compare V1 vs V2 behavior
- **No downtime**: System remains operational throughout

---

## Step-by-Step Migration Guide

### Step 1: Add Required Packages

Update your `.csproj` file:

```xml
<ItemGroup>
  <!-- Agent Framework (v2) -->
  <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.251016.1" />
  <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.0.0-preview.251016.1" />

  <!-- Core AI abstractions -->
  <PackageReference Include="Microsoft.Extensions.AI" Version="9.10.0" />

  <!-- Keep Semantic Kernel temporarily for coexistence -->
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.48.0" />
</ItemGroup>
```

### Step 2: Configure IChatClient in DI

In `Program.cs`, register `IChatClient`:

```csharp
// Register IChatClient for Agent Framework (v2) agents
builder.Services.AddSingleton<IChatClient>(provider =>
{
    var baseUri = new Uri(azureOpenAIEndpoint.TrimEnd('/'));
    var azureChatEndpoint = new Uri(baseUri,
        $"openai/deployments/{azureOpenAIDeploymentName}/?api-version={azureOpenAIApiVersion}");

    var chatClient = new OpenAI.Chat.ChatClient(
        model: azureOpenAIDeploymentName,
        credential: new System.ClientModel.ApiKeyCredential(azureOpenAIApiKey),
        options: new OpenAI.OpenAIClientOptions()
        {
            Endpoint = azureChatEndpoint
        });
    return chatClient.AsIChatClient();
});
```

**Important**: For structured output support, use API version `2024-08-01-preview` or later:

```csharp
var azureOpenAIApiVersion = builder.Configuration["AzureOpenAI:ApiVersion"]
    ?? "2024-08-01-preview"; // Required for structured output (json_schema)
```

### Step 3: Create Base Agent Class

Create a base class for common agent functionality:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace PartnershipAgent.Core.Agents;

public abstract class BaseAgent
{
    protected ChatClientAgent Agent { get; set; } = null!;
    protected IRequestedBy RequestedBy { get; }
    protected Guid ThreadId { get; }
    protected ILogger Logger { get; }

    public abstract string Name { get; }
    public abstract string Description { get; }

    protected BaseAgent(IRequestedBy requestedBy, Guid threadId, ILogger logger)
    {
        RequestedBy = requestedBy;
        ThreadId = threadId;
        Logger = logger;
    }

    protected async Task<ChatMessage> RunAsync(
        string message,
        ChatClientAgentRunOptions? options = null)
    {
        Logger.LogInformation("Running agent {Name} with ThreadId: {ThreadId}",
            Name, ThreadId);

        var response = await Agent.RunAsync(message, options);
        return response;
    }
}
```

### Step 4: Migrate Individual Agents

For each Semantic Kernel agent, create a V2 version:

**Before (Semantic Kernel):**
```csharp
public class ScopingAgent : BaseChatHistoryAgent
{
    public ScopingAgent(
        Kernel kernel,
        IChatHistoryService chatHistoryService,
        IRequestedBy requestedBy,
        Guid threadId,
        ILogger<ScopingAgent> logger
    ) : base(kernel, chatHistoryService, requestedBy, threadId, logger)
    {
        Name = "ScopingAgent";
        Instructions = "Your instructions...";
    }

    public async Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt)
    {
        var chatHistory = new ChatHistory(Instructions);
        chatHistory.AddUserMessage($"User Request: {prompt}");

        var chatCompletionService = Kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            kernel: Kernel
        );

        // Parse result...
    }
}
```

**After (Agent Framework):**
```csharp
public class ScopingAgentV2 : BaseAgent, IScopingAgent
{
    private readonly IChatClient _chatClient;
    private ChatOptions? _chatOptions;

    public override string Name => "ScopingAgent";
    public override string Description => "Determines request scope";

    public ScopingAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IRequestedBy requestedBy,
        ILogger<ScopingAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient;
        InitializeAgent();
    }

    private void InitializeAgent()
    {
        var instructions = "Your instructions...";

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });
    }

    public async Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt)
    {
        var agentMessage = $"User Request: {prompt}";
        var response = await RunAsync(agentMessage);

        // Parse response...
        return JsonSerializer.Deserialize<ScopingAgentResponse>(response.Text);
    }
}
```

### Step 5: Migrate Chat History Services

Update chat history services to use Agent Framework types:

**Before:**
```csharp
public async Task AddMessageToChatHistoryAsync(
    Guid threadId,
    ChatMessageContent message)
{
    await using var connection = _sqlConnectionFactory.CreateConnection();
    await connection.OpenAsync();

    command.Parameters.Add(CreateParameter(command, "@Role", message.Role.Label));
    command.Parameters.Add(CreateParameter(command, "@Content", message.Content));
    command.Parameters.Add(CreateParameter(command, "@ModelId", message.ModelId));

    await command.ExecuteNonQueryAsync();
}
```

**After:**
```csharp
public async Task AddMessageToChatHistoryAsync(
    Guid threadId,
    ChatMessage message)
{
    await using var connection = _sqlConnectionFactory.CreateConnection();
    await connection.OpenAsync();

    // ChatRole is accessed via .Value property
    command.Parameters.Add(CreateParameter(command, "@Role", message.Role.Value));

    // Content is accessed via .Text property
    command.Parameters.Add(CreateParameter(command, "@Content", message.Text));

    // ModelId stored in AdditionalProperties
    var modelId = message.AdditionalProperties?.TryGetValue("ModelId", out var val) == true
        ? val?.ToString()
        : null;
    command.Parameters.Add(CreateParameter(command, "@ModelId", modelId));

    await command.ExecuteNonQueryAsync();
}
```

### Step 6: Update Workflow Orchestration

Migrate workflow services to use V2 agents:

**Before:**
```csharp
await _chatHistoryService.AddMessageToChatHistoryAsync(
    threadId,
    new ChatMessageContent(AuthorRole.User, request.Prompt));

var scopingResult = await _scopingAgent.EvaluateScopeAsync(request.Prompt);
```

**After:**
```csharp
await _chatHistoryService.AddMessageToChatHistoryAsync(
    threadId,
    new ChatMessage(ChatRole.User, request.Prompt));

var scopingResult = await _scopingAgent.EvaluateScopeAsync(request.Prompt);
```

### Step 7: Exclude Old Files from Build

Once V2 agents are tested, exclude old Semantic Kernel files:

```xml
<!-- Exclude old Semantic Kernel agents/services from build -->
<ItemGroup>
  <Compile Remove="Agents\ScopingAgent.cs" />
  <Compile Remove="Agents\EntityResolutionAgent.cs" />
  <Compile Remove="Services\WorkflowOrchestrationService.cs" />
  <!-- Add other files to exclude -->
</ItemGroup>
```

### Step 8: Remove Semantic Kernel Dependencies

After confirming V2 works, remove Semantic Kernel packages:

```xml
<!-- Remove these -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.48.0" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Abstractions" Version="1.48.0" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.48.0" />
```

---

## Code Examples

### Example 1: Simple Agent Migration

**Semantic Kernel Version:**
```csharp
public class EntityResolutionAgent : BaseChatHistoryAgent
{
    public EntityResolutionAgent(
        Kernel kernel,
        IChatHistoryService chatHistoryService,
        IRequestedBy requestedBy,
        Guid threadId,
        ILogger<EntityResolutionAgent> logger
    ) : base(kernel, chatHistoryService, requestedBy, threadId, logger)
    {
        Name = "EntityResolutionAgent";
        Instructions = @"
Extract entities from user questions about partnerships.
Focus on: company names, person names, contract terms, financial terms.";
    }

    public async Task<List<ExtractedEntity>> ExtractEntities(string prompt)
    {
        var chatHistory = await GetChatHistoryAsync();
        chatHistory.AddSystemMessage(Instructions);
        chatHistory.AddUserMessage($"Extract entities from: {prompt}");

        var chatCompletionService = Kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            kernel: Kernel
        );

        // Manual parsing of text response
        return ParseEntitiesFromText(result.Content);
    }
}
```

**Agent Framework Version with Structured Output:**
```csharp
public class EntityResolutionAgentV2 : BaseAgent, IEntityResolutionAgent
{
    private readonly IChatClient _chatClient;
    private ChatOptions? _chatOptions;

    public override string Name => "EntityResolutionAgent";
    public override string Description => "Extracts entities from partnership queries";

    public EntityResolutionAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IRequestedBy requestedBy,
        ILogger<EntityResolutionAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient;
        InitializeAgent();
    }

    private void InitializeAgent()
    {
        var instructions = @"
Extract entities from user questions about partnerships.
Focus on: company names, person names, contract terms, financial terms.

For each entity, provide:
- text: The exact text of the entity
- type: One of: company, person, partnership_term, financial, date, contract_term, metric, general
- confidence: A number between 0 and 1";

        // Configure structured JSON output
        var chatOptions = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                AIJsonUtilities.CreateJsonSchema(typeof(EntityResolutionResponse)),
                "entity_extraction_response",
                "Entity extraction response for partnership queries"
            )
        };

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });

        _chatOptions = chatOptions;
    }

    public async Task<List<ExtractedEntity>> ExtractEntities(string prompt)
    {
        Logger.LogInformation("Extracting entities from prompt: {Prompt}", prompt);

        var agentMessage = $"Extract entities from: {prompt}";

        var options = new ChatClientAgentRunOptions
        {
            ChatOptions = _chatOptions
        };

        var response = await RunAsync(agentMessage, options: options);

        // Structured output guaranteed to match schema
        var entityResponse = JsonSerializer.Deserialize<EntityResolutionResponse>(
            response.Text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        return entityResponse?.ExtractedEntities ?? new List<ExtractedEntity>();
    }
}
```

### Example 2: Agent with Tool/Function Calling

**Agent Framework with Tools:**
```csharp
public class DocumentSearchAgentV2 : BaseAgent, IDocumentSearchAgent
{
    private readonly IChatClient _chatClient;
    private readonly IElasticSearchService _elasticSearchService;

    public DocumentSearchAgentV2(
        Guid threadId,
        IChatClient chatClient,
        IElasticSearchService elasticSearchService,
        IRequestedBy requestedBy,
        ILogger<DocumentSearchAgentV2> logger
    ) : base(requestedBy, threadId, logger)
    {
        _chatClient = chatClient;
        _elasticSearchService = elasticSearchService;
        InitializeAgent();
    }

    private void InitializeAgent()
    {
        var instructions = @"
You are a document search specialist. Use the search_documents tool to find
relevant partnership documents based on user queries.";

        Agent = new ChatClientAgent(
            _chatClient,
            new ChatClientAgentOptions
            {
                Name = Name,
                Instructions = instructions
            });
    }

    public async Task<List<RelevantDocument>> SearchDocuments(
        string query,
        List<ExtractedEntity> entities)
    {
        Logger.LogInformation("Searching documents for tenant {TenantId} with query: {Query}",
            RequestedBy.CompanyId, query);

        // Direct call to search service (no LLM needed for this operation)
        var results = await _elasticSearchService.SearchDocumentsAsync(
            query,
            RequestedBy.CompanyId,
            topK: 5
        );

        return results;
    }
}
```

---

## Common Patterns

### Pattern 1: Agent Initialization

Always initialize the `ChatClientAgent` in a dedicated method:

```csharp
private void InitializeAgent()
{
    Agent = new ChatClientAgent(
        _chatClient,
        new ChatClientAgentOptions
        {
            Name = Name,
            Instructions = instructions
        });
}
```

### Pattern 2: Running Agent with Options

Pass `ChatOptions` through `ChatClientAgentRunOptions`:

```csharp
var options = new ChatClientAgentRunOptions
{
    ChatOptions = new ChatOptions
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(...),
        Temperature = 0.7f,
        MaxOutputTokens = 1000
    }
};

var response = await RunAsync(message, options: options);
```

### Pattern 3: Error Handling with Fallback

```csharp
public async Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt)
{
    try
    {
        var response = await RunAsync($"User Request: {prompt}", options: options);

        var scopingResponse = JsonSerializer.Deserialize<ScopingAgentResponse>(
            response.Text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (scopingResponse == null)
        {
            Logger.LogWarning("Failed to deserialize, defaulting to in-scope");
            return CreateDefaultInScopeResponse();
        }

        return scopingResponse;
    }
    catch (Exception ex) when (LogException(ex, $"Error evaluating scope: {prompt}"))
    {
        // Default to in-scope to avoid blocking legitimate requests
        return CreateDefaultInScopeResponse();
    }
}
```

### Pattern 4: ModelId Storage in AdditionalProperties

Since `ChatMessage` doesn't have a `ModelId` property, store it in `AdditionalProperties`:

**Writing:**
```csharp
var message = new ChatMessage(ChatRole.Assistant, content);
message.AdditionalProperties["ModelId"] = "gpt-4";
await _chatHistoryService.AddMessageToChatHistoryAsync(threadId, message);
```

**Reading:**
```csharp
var modelId = message.AdditionalProperties?.TryGetValue("ModelId", out var value) == true
    ? value?.ToString()
    : null;
```

---

## Structured Output Implementation

One of the key advantages of the Agent Framework is first-class support for structured JSON output using Azure OpenAI's JSON schema feature.

### Prerequisites

1. **API Version**: Requires `2024-08-01-preview` or later
2. **Model**: Works with GPT-4 and GPT-3.5-turbo models

### Step 1: Define Response Model

Create a strongly-typed response model:

```csharp
using System.Text.Json.Serialization;

namespace PartnershipAgent.Core.Models;

public class ScopingAgentResponse
{
    /// <summary>
    /// Indicates whether the user's request is in scope.
    /// CRITICAL: Set to true if question mentions partnership, revenue, tier,
    /// agreement, contract, payment, partner, company, vendor, pricing, fees, etc.
    /// </summary>
    [JsonPropertyName("isInScope")]
    public bool IsInScope { get; set; }

    /// <summary>
    /// The confidence level of the scoping decision (high/medium/low).
    /// </summary>
    [JsonPropertyName("confidenceLevel")]
    public string ConfidenceLevel { get; set; } = "medium";

    /// <summary>
    /// Explanation of why the request is in or out of scope.
    /// </summary>
    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// The category of the request.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;
}
```

**Important**: The XML documentation comments become part of the JSON schema and provide guidance to the LLM. Use them to:
- Clarify ambiguous fields
- Provide examples
- Specify constraints
- Emphasize critical rules

### Step 2: Configure Structured Output

```csharp
private void InitializeAgent()
{
    var instructions = "Your agent instructions...";

    // Configure structured JSON output
    var chatOptions = new ChatOptions
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(
            AIJsonUtilities.CreateJsonSchema(typeof(ScopingAgentResponse)),
            "scoping_response",  // Schema name
            "Scoping response for partnership agent requests"  // Description
        )
    };

    Agent = new ChatClientAgent(
        _chatClient,
        new ChatClientAgentOptions
        {
            Name = Name,
            Instructions = instructions
        });

    _chatOptions = chatOptions;
}
```

### Step 3: Use Structured Output in Agent Calls

```csharp
public async Task<ScopingAgentResponse> EvaluateScopeAsync(string prompt)
{
    var options = new ChatClientAgentRunOptions
    {
        ChatOptions = _chatOptions  // Include structured output options
    };

    var response = await RunAsync($"User Request: {prompt}", options: options);

    // Response is guaranteed to match the schema
    var scopingResponse = JsonSerializer.Deserialize<ScopingAgentResponse>(
        response.Text,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    );

    return scopingResponse!;
}
```

### Benefits of Structured Output

1. **Type Safety**: Compile-time checking of response structure
2. **Reliability**: LLM response guaranteed to match schema
3. **No Parsing**: No need for regex, string manipulation, or fuzzy parsing
4. **Validation**: Schema validation happens at the API level
5. **Documentation**: XML comments provide inline guidance to the LLM

### Structured Output Best Practices

1. **Use descriptive property names**: `isInScope` is better than `result`
2. **Add detailed XML comments**: They become part of the schema description
3. **Include constraints in comments**: "Set to true if..." type instructions
4. **Use enums for limited choices**: Better than free-text fields
5. **Provide examples in comments**: Helps the LLM understand expected format
6. **Keep schemas focused**: One response model per agent operation

---

## Testing Strategy

### Unit Testing V2 Agents

```csharp
[Fact]
public async Task ScopingAgent_PartnershipQuestion_ReturnsInScope()
{
    // Arrange
    var mockChatClient = new MockChatClient();
    var logger = new Mock<ILogger<ScopingAgentV2>>();
    var requestedBy = new SimpleRequestedBy();
    var threadId = Guid.NewGuid();

    var agent = new ScopingAgentV2(threadId, mockChatClient, requestedBy, logger.Object);

    // Act
    var result = await agent.EvaluateScopeAsync(
        "What are the partnership revenue sharing tiers?"
    );

    // Assert
    Assert.True(result.IsInScope);
    Assert.Contains("partnership", result.Category, StringComparison.OrdinalIgnoreCase);
}
```

### Integration Testing

Test the full workflow with both V1 and V2 to compare behavior:

```csharp
[Fact]
public async Task Workflow_PartnershipQuestion_V2MatchesV1Behavior()
{
    // Arrange
    var request = new ChatRequest
    {
        Prompt = "What are the partnership revenue sharing tiers?"
    };

    // Act
    var v1Result = await _workflowServiceV1.ProcessRequestAsync(request);
    var v2Result = await _workflowServiceV2.ProcessRequestAsync(request);

    // Assert
    Assert.Equal(v1Result.IsInScope, v2Result.IsInScope);
    Assert.Equal(v1Result.EntityCount, v2Result.EntityCount);
    Assert.NotEmpty(v2Result.Response);
}
```

### End-to-End Testing

```bash
# Test scoping
curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"threadId": "test-1", "prompt": "What are the partnership revenue tiers?"}'

# Test out-of-scope handling
curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"threadId": "test-2", "prompt": "Tell me a joke"}'
```

---

## Troubleshooting

### Issue 1: "response_format value as json_schema is enabled only for api versions 2024-08-01-preview and later"

**Cause**: Azure OpenAI API version is too old for structured output.

**Solution**: Update API version in configuration:

```csharp
var azureOpenAIApiVersion = builder.Configuration["AzureOpenAI:ApiVersion"]
    ?? "2024-08-01-preview";
```

And in user secrets:
```bash
dotnet user-secrets set "AzureOpenAI:ApiVersion" "2024-08-01-preview"
```

### Issue 2: ChatMessage doesn't have ModelId property

**Cause**: `ChatMessage` in Agent Framework uses a different property model.

**Solution**: Store ModelId in `AdditionalProperties`:

```csharp
// Writing
message.AdditionalProperties["ModelId"] = modelId;

// Reading
var modelId = message.AdditionalProperties?.TryGetValue("ModelId", out var val) == true
    ? val?.ToString()
    : null;
```

### Issue 3: Structured output returns wrong values

**Cause**: LLM isn't following the schema constraints.

**Solution**: Add more detailed guidance in XML comments:

```csharp
/// <summary>
/// CRITICAL: Set to true if the question mentions 'partnership', 'revenue',
/// 'tier', 'agreement', 'contract', 'payment', or 'partner'.
/// Set to false only for clearly unrelated topics like jokes, weather, sports.
/// </summary>
[JsonPropertyName("isInScope")]
public bool IsInScope { get; set; }
```

### Issue 4: Type not found errors during build

**Cause**: Old Semantic Kernel files still being compiled.

**Solution**: Exclude them from compilation:

```xml
<ItemGroup>
  <Compile Remove="Agents\OldAgent.cs" />
</ItemGroup>
```

### Issue 5: Dependency injection fails for IChatClient

**Cause**: IChatClient not registered or registered incorrectly.

**Solution**: Ensure proper registration in Program.cs:

```csharp
builder.Services.AddSingleton<IChatClient>(provider =>
{
    var chatClient = new OpenAI.Chat.ChatClient(/*...*/);
    return chatClient.AsIChatClient();  // Important: call AsIChatClient()
});
```

---

## Lessons Learned

### What Went Well

1. **Coexistence Strategy**: Running V1 and V2 side-by-side reduced risk
2. **Structured Output**: Significantly improved reliability of agent responses
3. **Type Safety**: Caught many issues at compile-time vs runtime
4. **Incremental Migration**: Migrating one agent at a time made debugging easier
5. **Interface Preservation**: Keeping the same interfaces (`IScopingAgent`, etc.) made switching seamless

### Challenges Encountered

1. **API Version Requirements**: Structured output requires recent API versions
2. **Property Access Changes**: `ChatMessage` properties accessed differently than `ChatMessageContent`
3. **Documentation Gaps**: Agent Framework is preview software with limited docs
4. **Schema Guidance**: LLMs need very explicit instructions in schema comments
5. **Testing Complexity**: Need to test both structured and unstructured response paths

### Best Practices for Future Migrations

1. **Start with Read-Only Agents**: Migrate agents that don't modify state first
2. **Test Thoroughly**: Especially test error paths and edge cases
3. **Monitor Logs**: Watch for deserialization errors and unexpected responses
4. **Use Structured Output**: It's more reliable than text parsing
5. **Keep Old Code**: Don't delete until fully confident in V2
6. **Document Differences**: Track breaking changes and workarounds
7. **Update in Stages**: Don't try to migrate everything at once

### Recommended Migration Order

1. **Stateless agents** (e.g., ScopingAgent, EntityResolutionAgent)
2. **Service interfaces** (e.g., IChatHistoryService)
3. **Service implementations** (e.g., AzureSqlChatHistoryService)
4. **Orchestration services** (e.g., WorkflowService)
5. **Stateful agents** (e.g., ResponseGenerationAgent with streaming)
6. **Remove old dependencies**

---

## Additional Resources

- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/ai-extensions-overview)
- [Microsoft Agent Framework GitHub](https://github.com/microsoft/agents)
- [Azure OpenAI Structured Output](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/structured-outputs)
- [Migration GitHub Issue](https://github.com/microsoft/semantic-kernel/discussions) - Check for migration discussions

---

## Conclusion

Migrating from Semantic Kernel to the Agent Framework is a significant undertaking, but the benefits in code clarity, reliability, and maintainability make it worthwhile. The key to success is:

1. Use a phased migration approach
2. Leverage structured output for reliability
3. Test thoroughly at each stage
4. Keep old code until confident in new implementation

This migration guide reflects real-world experience and should provide a solid foundation for your own migration journey.
