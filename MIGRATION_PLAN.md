# Partnership Agent: Semantic Kernel → Microsoft Agent Framework Migration Plan

**Version 1.0 (Semantic Kernel)**: Tagged as `v1.0-semantic-kernel`
**Version 2.0 (Agent Framework)**: Target branch `wip/bobjac/agent-framework-convert`
**Created**: 2025-10-27

---

## Executive Summary

This document outlines the migration strategy from **Microsoft Semantic Kernel v1.48.0** to **Microsoft Agent Framework v1.0.0-preview** for the Partnership Agent project. The Agent Framework is positioned as "Semantic Kernel v2.0" and represents the unified future for building AI agents at Microsoft.

### Key Facts
- **Current State**: 1,500+ LOC using Semantic Kernel with advanced Process Framework (alpha)
- **Target State**: Microsoft Agent Framework (preview) with Workflows
- **Risk Level**: Medium-High (both frameworks have preview/alpha features)
- **Timeline**: Phased migration over multiple PRs
- **Rollback Strategy**: v1.0-semantic-kernel tag preserves stable baseline

---

## I. Architecture Analysis

### Current Semantic Kernel Architecture

```
User Request
    ↓
[WebApi/ConsoleApp]
    ↓
StepOrchestrationService
    ↓
ProcessBuilder → KernelProcess (5 Steps, Event-Driven)
    ↓
┌────────────────────────────────────────────────┐
│ ScopingStep → EntityResolutionStep            │
│      ↓              ↓                          │
│ DocumentSearchStep → ResponseGenerationStep    │
│                 ↓                              │
│           UserResponseStep                     │
└────────────────────────────────────────────────┘
    ↓
3 Specialized ChatCompletionAgents:
  - ScopingAgent (scope validation)
  - EntityResolutionAgent (entity extraction)
  - FAQAgent (document search + answer generation)
```

### Target Agent Framework Architecture

```
User Request
    ↓
[WebApi/ConsoleApp]
    ↓
WorkflowOrchestrationService
    ↓
WorkflowBuilder → Workflow<ChatMessage> (Edge-Based Graph)
    ↓
┌──────────────────────────────────────────────┐
│ ScopingAgent → EntityResolutionAgent         │
│      ↓              ↓                        │
│ DocumentSearchAgent → ResponseGenerationAgent│
│                 ↓                            │
│           UserResponseAgent                  │
└──────────────────────────────────────────────┘
    ↓
5 ChatClientAgent instances:
  - ScopingAgent (scope validation)
  - EntityResolutionAgent (entity extraction)
  - DocumentSearchAgent (document retrieval)
  - ResponseGenerationAgent (answer generation)
  - UserResponseAgent (final delivery)
```

**Key Architectural Change**: Event-driven steps → Graph edges with direct agent connections

---

## II. Component Mapping: SK → Agent Framework

| Current Component | SK Type | Target Component | AF Type | Migration Complexity |
|-------------------|---------|------------------|---------|---------------------|
| **BaseChatHistoryAgent** | Abstract base extending ChatHistoryAgent | **BaseAgent** | Abstract wrapper around ChatClientAgent | Medium (refactor base class) |
| **ScopingAgent** | ChatCompletionAgent | **ScopingAgent** | ChatClientAgent | Low (mostly config) |
| **EntityResolutionAgent** | ChatCompletionAgent | **EntityResolutionAgent** | ChatClientAgent | Low (mostly config) |
| **FAQAgent** | ChatCompletionAgent | **DocumentSearchAgent + ResponseGenerationAgent** | Two ChatClientAgent instances | Medium (split responsibilities) |
| **StepOrchestrationService** | ProcessBuilder orchestration | **WorkflowOrchestrationService** | WorkflowBuilder orchestration | High (paradigm shift) |
| **ScopingStep** | KernelProcessStep | **Workflow edge + ScopingAgent** | Agent in graph | Medium (convert step logic) |
| **EntityResolutionStep** | KernelProcessStep | **Workflow edge + EntityResolutionAgent** | Agent in graph | Medium (convert step logic) |
| **DocumentSearchStep** | KernelProcessStep | **Workflow edge + DocumentSearchAgent** | Agent in graph | Medium (convert step logic) |
| **ResponseGenerationStep** | KernelProcessStep | **Workflow edge + ResponseGenerationAgent** | Agent in graph | Medium (convert step logic) |
| **UserResponseStep** | KernelProcessStep | **Workflow edge + UserResponseAgent** | Agent in graph | Low (terminal step) |
| **Kernel** | Core orchestrator | **Removed** | N/A (agents self-contained) | High (remove all Kernel references) |
| **KernelFunction** decorator | `[KernelFunction]` attribute | **AIFunctionFactory** | Direct function creation | Low (minimal changes) |
| **InvokeAsync/InvokeStreamingAsync** | Agent invocation | **RunAsync/RunStreamingAsync** | Agent invocation | Low (rename + signature change) |
| **KernelProcessEvent** | Event emission | **Workflow edges** | Graph routing | High (conceptual change) |
| **ChatHistory** | Manual management | **AgentThread** | Auto-managed by agent | Medium (simplification) |
| **OpenAIPromptExecutionSettings** | Complex nested config | **ChatClientAgentRunOptions** | Simplified config | Low (flatten config) |

---

## III. Detailed Migration Strategy

### Phase 1: Foundation & Setup (Week 1)

**Goal**: Set up Agent Framework dependencies and create basic agent infrastructure

**Tasks**:
1. ✅ Create migration branch `wip/bobjac/agent-framework-convert`
2. ✅ Tag v1.0 as `v1.0-semantic-kernel`
3. Add Agent Framework NuGet packages:
   ```xml
   <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-preview.251016.1" />
   <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.0.0-preview.251016.1" />
   <PackageReference Include="Microsoft.Extensions.AI" Version="9.10.0" />
   <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.10.0" />
   ```
4. Create new `BaseAgent` wrapper class (Agent Framework equivalent of `BaseChatHistoryAgent`)
5. Update DI configuration in `Program.cs` to support both patterns (temporary)

**Acceptance Criteria**:
- Solution builds successfully with Agent Framework packages
- No breaking changes to existing SK code (parallel setup)
- `BaseAgent` class created with proper abstractions

**Risk**: Low - Additive changes only

---

### Phase 2: Agent Migration (Week 2-3)

**Goal**: Migrate 3 ChatCompletionAgents → ChatClientAgent pattern

#### Phase 2A: ScopingAgent Migration
**Complexity**: Low

**Changes**:
```csharp
// BEFORE (SK)
public class ScopingAgent : BaseChatHistoryAgent
{
    Agent = new ChatCompletionAgent
    {
        Instructions = scopingInstructions,
        Kernel = kernel,
        Arguments = new KernelArguments(executionSettings)
    };

    [KernelFunction]
    public async Task<ScopingAgentResponse> DetermineScope(string prompt) { ... }
}

// AFTER (AF)
public class ScopingAgent : BaseAgent
{
    Agent = chatClient.CreateAIAgent(
        name: "ScopingAgent",
        instructions: scopingInstructions,
        tools: [AIFunctionFactory.Create(DetermineScope)]
    );

    public async Task<ScopingAgentResponse> DetermineScope(
        [Description("User's request to evaluate")] string prompt) { ... }
}
```

**Tasks**:
1. Remove `Kernel` dependency from ScopingAgent
2. Replace `ChatCompletionAgent` with `ChatClientAgent` via `CreateAIAgent()`
3. Convert `[KernelFunction]` to `AIFunctionFactory.Create()`
4. Update `InvokeAsync()` → `RunAsync()` pattern
5. Update unit tests for ScopingAgent
6. Create integration test comparing v1 vs v2 outputs

**Acceptance Criteria**:
- ScopingAgent works identically with Agent Framework
- All unit tests pass
- Integration test shows equivalent responses

#### Phase 2B: EntityResolutionAgent Migration
**Complexity**: Low

**Tasks**: (Same pattern as ScopingAgent)
1. Remove Kernel dependency
2. Replace ChatCompletionAgent with ChatClientAgent
3. Convert function decorators
4. Update invocation methods
5. Update tests

#### Phase 2C: FAQAgent Split Migration
**Complexity**: Medium-High

**Changes**: Split FAQAgent into two focused agents:

```csharp
// NEW: DocumentSearchAgent
public class DocumentSearchAgent : BaseAgent
{
    // Responsibilities:
    // - SearchDocuments kernel function
    // - ElasticSearch/AzureVectorSearch integration
    // - Returns List<DocumentResult>
}

// NEW: ResponseGenerationAgent
public class ResponseGenerationAgent : BaseAgent
{
    // Responsibilities:
    // - GenerateAnswer function
    // - Citation formatting
    // - Streaming support via IBidirectionalToClientChannel
    // - Response evaluation
}
```

**Rationale**:
- Current FAQAgent has 2 kernel functions (search + generation) → better as separate agents
- Aligns with Agent Framework's "single responsibility" agent pattern
- Enables better workflow orchestration

**Tasks**:
1. Create `DocumentSearchAgent` with search capabilities
2. Create `ResponseGenerationAgent` with answer generation
3. Migrate `SearchDocuments()` function to DocumentSearchAgent
4. Migrate `GenerateAnswer()` function to ResponseGenerationAgent
5. Update ChatHistoryService integration
6. Update tests for both new agents

**Acceptance Criteria**:
- DocumentSearchAgent successfully retrieves documents
- ResponseGenerationAgent produces equivalent answers
- Streaming still works with IBidirectionalToClientChannel
- Citations maintain same quality

---

### Phase 3: Workflow Migration (Week 4-5)

**Goal**: Replace KernelProcess event-driven orchestration with WorkflowBuilder graph

**Complexity**: High - This is the most complex migration step

#### Current Event-Driven Flow
```csharp
ProcessBuilder processBuilder = new("PartnershipAgent");
var scopingStep = processBuilder.AddStepFromType<ScopingStep>();
// ... add all steps ...

processBuilder
    .OnInputEvent(AgentOrchestrationEvents.StartProcess)
    .SendEventTo(new ProcessFunctionTargetBuilder(scopingStep));

scopingStep
    .OnEvent(AgentOrchestrationEvents.ScopingCompleted)
    .SendEventTo(new ProcessFunctionTargetBuilder(entityResolutionStep));

// Complex event routing with early termination support
```

#### Target Workflow Graph
```csharp
// Create agents
var scopingAgent = CreateScopingAgent();
var entityAgent = CreateEntityResolutionAgent();
var searchAgent = CreateDocumentSearchAgent();
var responseAgent = CreateResponseGenerationAgent();
var userResponseAgent = CreateUserResponseAgent();

// Build workflow with edges
WorkflowBuilder builder = new(scopingAgent);

// Add edges to connect agents
builder.AddEdge(scopingAgent, entityAgent,
    condition: (msg) => msg.IsInScope); // Conditional routing

builder.AddEdge(scopingAgent, userResponseAgent,
    condition: (msg) => !msg.IsInScope); // Out-of-scope short-circuit

builder.AddEdge(entityAgent, searchAgent);
builder.AddEdge(searchAgent, responseAgent,
    condition: (msg) => msg.DocumentsFound);

builder.AddEdge(searchAgent, userResponseAgent,
    condition: (msg) => !msg.DocumentsFound); // Clarification needed

builder.AddEdge(responseAgent, userResponseAgent);

Workflow<ChatMessage> workflow = builder.Build<ChatMessage>();
```

**Migration Tasks**:

1. **Create WorkflowOrchestrationService**:
   - Replace `StepOrchestrationService`
   - Implement `WorkflowBuilder` setup
   - Define all agent connections via edges

2. **Implement Conditional Routing**:
   - Map event types to edge conditions
   - Handle early termination (out-of-scope, no documents)
   - Maintain equivalent flow logic

3. **State Management**:
   - Replace `ProcessModel` with workflow state
   - Use `context.ReadStateAsync()` and `context.WriteStateAsync()`
   - Ensure data flows correctly between agents

4. **Streaming Support**:
   - Integrate `IBidirectionalToClientChannel` with workflow
   - Handle `AgentRunUpdateEvent` for streaming chunks
   - Maintain real-time client communication

5. **Error Handling**:
   - Replace `ProcessError` events with workflow error handling
   - Implement try-catch around workflow execution
   - Graceful degradation for partial failures

6. **Remove Step Classes**:
   - Delete `BaseKernelProcessStep.cs`
   - Delete all 5 step implementations (ScopingStep, EntityResolutionStep, etc.)
   - Move any step-specific logic into agents or orchestration service

**Acceptance Criteria**:
- Workflow executes agents in correct order
- Conditional routing works (out-of-scope, no documents, etc.)
- State passes between agents correctly
- Streaming maintains real-time performance
- Error handling covers all scenarios
- End-to-end integration tests pass

---

### Phase 4: Dependency Injection & Configuration (Week 6)

**Goal**: Update DI container and configuration for Agent Framework

**Changes to Program.cs**:

```csharp
// BEFORE (SK)
builder.Services.AddScoped<IKernelBuilder>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddAzureOpenAIChatCompletion(...);
    return kernelBuilder;
});

builder.Services.AddScoped<ScopingAgent>(provider =>
{
    var kernelBuilder = provider.GetRequiredService<IKernelBuilder>();
    return new ScopingAgent(Guid.NewGuid(), kernelBuilder, ...);
});

// AFTER (AF)
builder.Services.AddSingleton<IChatClient>(provider =>
{
    var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
    return azureClient.GetChatClient(deploymentName);
});

builder.Services.AddScoped<ScopingAgent>(provider =>
{
    var chatClient = provider.GetRequiredService<IChatClient>();
    return new ScopingAgent(chatClient, ...);
});

builder.Services.AddScoped<WorkflowOrchestrationService>();
```

**Tasks**:
1. Remove `IKernelBuilder` registration
2. Add `IChatClient` registration with Azure OpenAI
3. Update all agent registrations to use IChatClient
4. Update `WorkflowOrchestrationService` registration
5. Remove step registrations (no longer needed)
6. Update configuration validation

**Acceptance Criteria**:
- DI container resolves all agents correctly
- ChatClient properly configured with Azure OpenAI
- No Kernel dependencies remain

---

### Phase 5: Testing & Validation (Week 7)

**Goal**: Comprehensive testing to ensure migration maintains functionality

**Test Strategy**:

1. **Unit Tests**:
   - Update all agent unit tests for new API patterns
   - Test workflow routing logic
   - Test conditional edge execution
   - Test error handling

2. **Integration Tests**:
   - Create side-by-side tests comparing v1 (SK) vs v2 (AF) outputs
   - Test full workflow execution end-to-end
   - Test streaming responses
   - Test all routing paths (in-scope, out-of-scope, no documents, etc.)

3. **Performance Tests**:
   - Compare response times SK vs AF
   - Measure streaming latency
   - Load testing (if applicable)

4. **Manual Testing**:
   - Test via WebAPI endpoints
   - Test via ConsoleApp
   - Verify OpenTelemetry traces still work
   - Verify chat history persistence (SQLite, Azure SQL)

**Acceptance Criteria**:
- All unit tests pass (100% coverage maintained)
- Integration tests show equivalent or better results
- No performance regressions
- All user-facing features work identically

---

### Phase 6: Documentation & Cleanup (Week 8)

**Goal**: Update documentation and remove SK remnants

**Tasks**:
1. Update README.md with Agent Framework references
2. Update architecture documentation
3. Update evaluation documentation (if affected)
4. Remove all SK packages from .csproj files
5. Remove SK-specific code comments
6. Update API documentation
7. Update setup scripts if needed
8. Create CHANGELOG.md entry for v2.0

**Acceptance Criteria**:
- Documentation reflects Agent Framework architecture
- No SK references remain in docs
- Setup/installation instructions updated
- Changelog documents all breaking changes

---

## IV. Risk Assessment & Mitigation

### High Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Agent Framework API changes during migration** | High | Medium | Monitor GitHub releases; pin to specific preview version |
| **Workflow orchestration doesn't support all SK Process features** | High | Medium | Prototype complex routing early; file issues with Microsoft if needed |
| **Performance regression in streaming** | Medium | Low | Performance tests in Phase 5; optimize before merging |
| **Breaking changes in Agent Framework before GA** | High | High | Stay on v1 until AF reaches GA; delay production deployment |

### Medium Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Chat history integration issues** | Medium | Low | Test early with all backends (InMemory, SQLite, Azure SQL) |
| **OpenTelemetry tracing gaps** | Medium | Low | Verify telemetry in Phase 5; add custom instrumentation if needed |
| **Tool/function calling behavior differences** | Medium | Medium | Integration tests comparing function execution |

### Low Risks

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **Agent instructions require tuning** | Low | Medium | A/B test with evaluation framework |
| **DI container configuration issues** | Low | Low | Unit tests for service resolution |

---

## V. Rollback Strategy

### If Migration Fails or Blocks

1. **Immediate Rollback**:
   ```bash
   git checkout main
   git branch -D wip/bobjac/agent-framework-convert
   git checkout v1.0-semantic-kernel -b wip/bobjac/agent-framework-convert
   # Resume from stable baseline
   ```

2. **Partial Rollback**:
   - Each phase is a separate PR (if desired)
   - Can revert specific commits while keeping others

3. **Production Strategy**:
   - Keep v1 (SK) in production until v2 (AF) fully validated
   - Run both versions in parallel during transition
   - Use feature flags to toggle between implementations

---

## VI. Success Criteria

### Must-Have (Blocking)
- ✅ All agents migrate to ChatClientAgent
- ✅ Workflow orchestration replaces Process Framework
- ✅ All unit tests pass
- ✅ Integration tests show equivalent functionality
- ✅ No Semantic Kernel dependencies remain
- ✅ Streaming works correctly

### Nice-to-Have (Non-Blocking)
- Performance improvements over SK
- Simplified codebase (fewer LOC)
- Better observability via AF tooling
- Workflow debugging in DevUI

---

## VII. Timeline Summary

| Phase | Duration | Status | Complexity |
|-------|----------|--------|------------|
| Phase 1: Foundation & Setup | Week 1 | 🟢 Ready | Low |
| Phase 2: Agent Migration | Week 2-3 | 🟡 Planned | Low-Medium |
| Phase 3: Workflow Migration | Week 4-5 | 🔴 High Risk | High |
| Phase 4: DI & Configuration | Week 6 | 🟡 Planned | Medium |
| Phase 5: Testing & Validation | Week 7 | 🟡 Planned | Medium |
| Phase 6: Documentation & Cleanup | Week 8 | 🟢 Ready | Low |

**Total Estimated Duration**: 8 weeks (phased, part-time)
**Critical Path**: Phase 3 (Workflow Migration)

---

## VIII. Key Decisions & Open Questions

### Decisions Made
- ✅ Split FAQAgent into DocumentSearchAgent + ResponseGenerationAgent
- ✅ Use WorkflowBuilder with edge-based routing (not events)
- ✅ Maintain v1 tag for rollback safety
- ✅ Phased migration over single big-bang rewrite

### Open Questions
- ❓ Should we wait for Agent Framework GA before production deployment?
- ❓ Do we need custom workflow executors or are agents sufficient?
- ❓ How to handle evaluation framework integration with AF?
- ❓ Should we maintain both v1 and v2 branches long-term or merge after validation?

---

## IX. References

### Agent Framework Documentation
- [Migration Guide from SK](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-semantic-kernel/)
- [Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Workflows Documentation](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/using-agents)
- [GitHub Repository](https://github.com/microsoft/agent-framework)

### Internal Documentation
- Partnership Agent v1 Architecture (from exploration analysis)
- Semantic Kernel Process Framework docs
- Chat History Service documentation

---

## X. Next Steps

**Immediate Actions**:
1. Review this migration plan with stakeholders
2. Decide on GA vs Preview deployment strategy
3. Begin Phase 1: Foundation & Setup
4. Create tracking issues for each phase

**Before Starting Phase 3**:
- Prototype workflow routing with minimal example
- Validate conditional edge logic works as expected
- Confirm state management pattern

---

**Document Version**: 1.0
**Last Updated**: 2025-10-27
**Owner**: Partnership Agent Team
**Status**: 📋 Ready for Review
