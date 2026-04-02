# AGENTS.md — LLM-Client-Sharp Contributor Guide

## Project Overview

**LLM Client for WPF** is a .NET 10 WPF application providing a rich LLM chat client with RAG, MCP, agent workflows, and code-awareness features. The primary project is `LLMClient/LLMClient.csproj`; tests live in `LLMClient.Test/`.

---

## Repository Layout

```
LLMClient/                  # Main WPF application
  Abstraction/              # Core interfaces and base types
  Agent/                    # IAgent implementations (MiniSWE, Search, PromptBased)
  Component/                # Reusable UI controls, converters, BaseViewModel
  Configuration/            # GlobalOptions, proxy, prompts resource
  ContextEngineering/       # Roslyn analysis + code search/reading plugins
  Data/                     # Persistence models + LLM_DataSerializeContext
  Dialog/                   # Chat session UI/VM, dialog history item models
  Endpoints/                # Endpoint/client implementations (OpenAI API, Azure/GitHub Copilot)
  Log/                      # DailyRollingLogSink, logger provider, trace listener
  Project/                  # Multi-dialog Project feature (C#, C++, General)
  Rag/                      # RAG pipeline, document parsers, SemanticKernelStore
  Resources/                # Fonts, images, grammars, shaders
  ToolCall/                 # Function groups, built-in plugins, MCP integration
  Workflow/                 # Agent orchestration (CoreAgents, Dynamic, Research)
LLMClient.Test/             # xUnit test project
LLMClient.Avalonia/         # Future Avalonia cross-platform port (stub)
```

---

## Build & Run

**SDK requirement:** .NET 10 preview (`global.json` sets `"version": "10.0.0"`, `"allowPrerelease": true`).

```powershell
# Build the solution
dotnet build AIClient.sln

# Run tests
dotnet test LLMClient.Test/LLMClient.Test.csproj

# Publish (run from LLMClient/ directory)
dotnet publish .\LLMClient.csproj -p:PublishProfile=FolderProfile
```

- Target framework: `net10.0-windows10.0.19041.0`
- Platform: `x64` (Release always targets `win-x64`)
- `EmitCompilerGeneratedFiles=true` — source-generator output lands in `obj/` and is compiled

---

## Suppressed Warnings

The project intentionally suppresses two diagnostic codes project-wide (`LLMClient.csproj`):

| Code | Reason |
|------|--------|
| `OPENAI001` | Experimental OpenAI client API |
| `SCME0001` | Experimental Semantic Kernel API |

Do not add `#pragma warning disable` for these — they are already globally suppressed.

---

## Architecture & Key Patterns

### MVVM — `BaseViewModel`

All view models extend `LLMClient.Component.ViewModel.Base.BaseViewModel` (`INotifyPropertyChanged`).

- Use `SetField(ref _field, value)` for synchronous properties.
- Use `GetAsyncProperty<T>(factory)` / `SetAsyncProperty<T>(value)` for lazily-computed async properties (e.g., token counts, async icon loading). These fire-and-forget internally and notify the UI on the dispatcher thread.
- Use `Dispatch(action)` / `await DispatchAsync(action)` to marshal work back to the UI thread.
- `BaseViewModel.ServiceLocator` is a static service-locator set in `Program.cs` after DI container build. This is an acknowledged anti-pattern; use constructor DI wherever possible.

### Dependency Injection — `Program.cs`

All singleton and transient registrations are in `Program.cs` → `ServiceCollection`. Add new services there. Example singletons: `IEndpointService`, `IRagSourceCollection`, `IMcpServiceCollection`, `BuiltInFunctionsCollection`, `Summarizer`.

AutoMapper profiles must be registered as `Profile` singletons and added via `.AddMap()` extension; current profiles: `DialogItemPersistenceProfile`, `DialogMappingProfile`, `SessionProjectPersistenceProfile`, `FunctionGroupPersistenceProfile`, `RoslynMappingProfile`.

### Chat Request Flow

```
DefaultDialogContextBuilder.BuildAsync(model)
  → registers function groups + RAG sources into KernelPluginCollection
  → builds List<ChatMessage> (applies system prompt, history)
  → returns RequestContext { ChatHistory, FunctionCallEngine, RequestOptions }

IChatEndpoint.SendRequest(RequestContext, IInvokeInteractor?, CancellationToken)
  → LlmClientBase subclass calls IChatClient + FunctionCallEngine loop
  → returns ChatCallResult
```

`DefaultDialogContextBuilder` is in `Abstraction/`; extend or override it for agent-specific context building (e.g., `AgentDialogContextBuilder` in `Agent/MiniSWE/`).

### Function Call Engine

`FunctionCallEngine` (abstract, `Abstraction/`) has two concrete implementations:

| Type | Class | When Used |
|------|-------|-----------|
| `Default` | `DefaultFunctionCallEngine` | Native tool-call support (`SupportFunctionCall = true`) |
| `Prompt` | `PromptFunctionCallEngine` | Text-based fallback for models without native tool-call |

The engine is selected automatically in `DefaultDialogContextBuilder.EnsureCallEngine()` based on `IEndpointModel.SupportFunctionCall`.

### Persistence

- JSON files written atomically via `JsonFileHelper.SaveJsonToFileAsync<T>()` (write to `.tmp`, then `File.Move`). Always use this helper — never `File.WriteAllText` directly for session/config data.
- Source-generated JSON context: `LLM_DataSerializeContext` (`Data/LLM_DataSerializeContext.cs`). When adding a new persist model, register its type with `[JsonSerializable(typeof(YourModel))]` in that partial class.
- Vector store: local SQLite (`identifier.sqlite`) via `Microsoft.SemanticKernel.Connectors.SqliteVec`. Embedding dimension is fixed at **1536** (`SemanticKernelStore.ChunkDimension`).

### Logging

Logging is configured in `Program.cs`:

- **DEBUG**: `LogLevel.Trace`, OpenTelemetry console exporter + `DailyRollingFileLoggerProvider`.
- **RELEASE**: `LogLevel.Information`, file-only via `DailyRollingFileLoggerProvider`; `Trace.*` calls are routed through `LoggerTraceListener` → ILogger pipeline.
- Log files are written to the `Logs/` directory relative to the executable.
- Use `ILogger<T>` via DI; do not call `Trace.TraceInformation` for new code (legacy pattern).

---

## Key Abstractions

| Interface | Location | Purpose |
|-----------|----------|---------|
| `ILLMAPIEndpoint` | `Abstraction/` | Endpoint container (holds models, creates clients) |
| `IChatEndpoint` | `Abstraction/` | Single model chat client — `SendRequest(RequestContext, ...)` |
| `ILLMChatClient` | `Abstraction/` | Extended chat client with model/params properties |
| `IEndpointModel` | `Abstraction/` | Model capability flags (`SupportFunctionCall`, `SupportSystemPrompt`, …) |
| `IChatRequest` | `Abstraction/` | Chat request descriptor (history, system prompt, function groups, RAG sources) |
| `ILLMSession` | `Abstraction/` | Saveable/clonable session contract |
| `IAIFunctionGroup` | `Abstraction/` | Named group of AI functions (tools) |
| `IRagSource` | `Abstraction/` | RAG source exposing functions as `IAIFunctionGroup` |
| `IMcpServiceCollection` | `Abstraction/` | MCP server registry |
| `IAgent` | `Agent/` | Agent interface for autonomous task execution |

---

## Adding an Endpoint

1. Implement `ILLMAPIEndpoint` (or extend `AzureEndPointBase` for Azure-family endpoints).
2. Implement `LlmClientBase` for the chat client; override `GetChatClient()` to return an `IChatClient`.
3. Register the endpoint type in `EndpointConfigureViewModel` if it needs UI configuration.
4. Provide endpoint-specific `View`/`ViewModel` pairs following the pattern in `Endpoints/OpenAIAPI/` and `Endpoints/Azure/`.

---

## Adding a Built-in Plugin (Tool)

1. Create a class in `ToolCall/DefaultPlugins/` with `[KernelFunction]` methods.
2. Add a persist model and register `[JsonSerializable(typeof(YourPluginPersistModel))]` in `LLM_DataSerializeContext`.
3. Add the plugin to `BuiltInFunctionsCollection` in `ToolCall/DefaultPlugins/BuiltInFunctionsCollection.cs`.

---

## RAG Pipeline

Files supported: PDF (`Karl.PdfPig`), Markdown, Word, Text, Excel.

Flow: `RagFileBase` subclass → parse into `ChunkNode` tree → embed via OpenAI embeddings → store in `SemanticKernelStore` (SQLite). Search algorithms available: `Default` (flat), `TopDown` (hierarchical from bookmark structure), `Recursive`.

RAG sources are exposed as `IAIFunctionGroup` implementations, so they participate in the standard function-call pipeline.

---

## MCP Support

MCP servers are managed by `MCPServiceCollection` (`ToolCall/MCP/`). Both `StdIOServerItem` and `SseServerItem` are supported. The JSON config format mirrors Claude Code's `mcp` schema.

---

## Testing

Test framework: **xUnit** (`LLMClient.Test/`).
t![img.png](img.png)esting are stored in `StreamingResponse.txt`.
- Moq is available for mocking interfaces.
- Tests that hit live APIs (e.g., `APITest.cs`) require environment credentials and are not run in CI.

---

## Workflow / Agent System

| Directory | Contents |
|-----------|----------|
| `Workflow/CoreAgents/` | `CoderAgent`, `InspectorAgent`, `ReviewerAgent` |
| `Workflow/Dynamic/` | `DynamicWorkflowEngine`, `WorkflowArchitect`, `WorkflowBlueprint` (LLM-planned execution) |
| `Workflow/Research/` | `ResearchClient`, `NvidiaResearchClient`, UI |
| `Workflow/Scheme/` | Shared data types: `TaskContract`, `PlanStep`, `DesignCandidate`, `ChangedFile` |
| `Agent/MiniSWE/` | `MiniSweAgent` — SWE-bench-style coding agent |

`IAgentStep` is the unit of execution in `DynamicWorkflowEngine`. `WorkflowContext` holds shared state across steps (`SharedMemory`).

---

## Context Engineering

`ContextEngineering/` provides Roslyn-based code awareness:

- `RoslynProjectAnalyzer` — analyzes MSBuild solutions/projects; call `AnalyzerExtension.RegisterMsBuild()` at startup (already done in `Program.cs`).
- Plugins in `ContextEngineering/Tools/`: `CodeReadingPlugin`, `CodeSearchPlugin`, `ProjectAwarenessPlugin`, `SymbolSemanticPlugin` — expose Roslyn queries as kernel functions.
- `ContextEngineering/PromptGeneration/` — formats analysis results into LLM prompts (`MarkdownSummaryFormatter`, `FileTreeFormatter`).

---

## Conventions

- Namespace matches directory path: `LLMClient.<SubFolder>` (e.g., `LLMClient.Rag`, `LLMClient.Workflow.Dynamic`).
- One class per file; XAML views paired with `.xaml.cs` code-behind; view models in separate files.
- Observable collections use `ObservableCollection<T>`; never replace — mutate in place on the UI thread.
- Async methods returning `void` only for fire-and-forget event handlers; otherwise return `Task`.
- `CancellationToken` must be threaded through async call chains; do not ignore tokens in loops.
- Do not add `async/await` wrappers around already-async code without adding value.

