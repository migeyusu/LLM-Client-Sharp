using System.Text;
using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;
/// <summary>
/// Agent-oriented dialog context that owns prompt construction.
/// Unlike the default chat context, this context renders a dedicated
/// agent system template and instance template, and treats tools/RAG/platform
/// information as prompt materials rather than directly appending them in chat style.
/// <example>
///  <code>
/// var agentContext = new AgentDialogContext(history)
/// {
/// SystemTemplate = systemTemplate,
/// InstanceTemplate = instanceTemplate,
/// PlatformId = "windows",
/// IncludeHistoryMessages = true,
/// IncludeToolInstructions = true,
/// IncludeRagInstructions = true,
/// FunctionGroups = request.FunctionGroups,
/// RagSources = request.RagSources,
/// SearchOption = request.SearchOption,
/// WorkingDirectory = request.WorkingDirectory,
/// CallEngineType = request.CallEngineType,
/// ResponseFormat = request.ResponseFormat,
/// TempAdditionalProperties = request.TempAdditionalProperties,
/// IsDebugMode = request.IsDebugMode
/// };
/// agentContext.MapFromRequest(request);
/// </code>
/// </example>
/// </summary>
public class AgentDialogContext : DialogContext
{
    public AgentDialogContext(IReadOnlyList<IChatHistoryItem> dialogItems) : base(dialogItems)
    {
    }

    public string SystemTemplate { get; set; } = string.Empty;

    public string InstanceTemplate { get; set; } = string.Empty;

    public bool IncludeHistoryMessages { get; set; } = true;

    /// <summary>
    /// Optional platform identifier. Defaults to "windows".
    /// </summary>
    public string PlatformId { get; set; } = "windows";

    /// <summary>
    /// If true, tool instructions are rendered into the agent prompt.
    /// If false, the agent template is considered fully self-contained.
    /// </summary>
    public bool IncludeToolInstructions { get; set; } = true;

    /// <summary>
    /// If true, RAG instructions are rendered into the agent prompt.
    /// </summary>
    public bool IncludeRagInstructions { get; set; } = true;

    /// <summary>
    /// Additional template variables for agent rendering.
    /// </summary>
    public Dictionary<string, object?> TemplateVariables { get; } = new();

    protected override string? BuildSystemPrompt()
    {
        // Agent context does not use the default chat-style system prompt assembly.
        // The actual system prompt is rendered in BuildChatHistoryAsync.
        return null;
    }

    protected override void AppendFunctionGroupsPrompt(StringBuilder builder)
    {
        // Intentionally ignored.
        // Agent prompt assembly uses structured tool instructions instead.
    }

    protected override void AppendRagSourcesPrompt(StringBuilder builder)
    {
        // Intentionally ignored.
        // Agent prompt assembly uses structured RAG instructions instead.
    }

    protected override async Task<List<ChatMessage>> BuildChatHistoryAsync(
        IEndpointModel model,
        IInvokeInteractor? interactor,
        FunctionCallEngine functionCallEngine,
        CancellationToken cancellationToken)
    {
        var chatHistory = new List<ChatMessage>();

        var historyMessages = IncludeHistoryMessages
            ? await GetMessagesAsync(cancellationToken)
            : new List<ChatMessage>();

        var templateVariables = await BuildTemplateVariablesAsync(model, cancellationToken);
        var renderedSystemPrompt = await RenderSystemTemplateAsync(templateVariables);
        var renderedInstancePrompt = await RenderInstanceTemplateAsync(templateVariables);

        if (!string.IsNullOrWhiteSpace(renderedSystemPrompt))
        {
            if (model.SupportSystemPrompt)
            {
                chatHistory.Add(new ChatMessage(ChatRole.System, renderedSystemPrompt));
            }
            else
            {
                interactor?.Warning(
                    "System prompt is not supported by this model. The rendered agent system prompt will be inserted as a user message.");
                chatHistory.Add(new ChatMessage(ChatRole.User, renderedSystemPrompt));
            }
        }

        if (!string.IsNullOrWhiteSpace(renderedInstancePrompt))
        {
            chatHistory.Add(new ChatMessage(ChatRole.User, renderedInstancePrompt));
        }

        if (historyMessages.Count > 0)
        {
            chatHistory.AddRange(historyMessages);
        }

        return chatHistory;
    }

    protected virtual async Task<Dictionary<string, object?>> BuildTemplateVariablesAsync(
        IEndpointModel model,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["task"] = UserPrompt ?? string.Empty,
            ["platform_id"] = PlatformId,
            ["platform_instructions"] = BuildPlatformInstructions(),
            ["tool_instructions"] = ShouldRenderToolInstructions()
                ? BuildToolInstructions()
                : string.Empty,
            ["rag_instructions"] = ShouldRenderRagInstructions()
                ? BuildRagInstructions()
                : string.Empty,
            ["tool_selection_guidance"] = BuildToolSelectionGuidance(),
            ["working_directory"] = WorkingDirectory ?? string.Empty,
            ["system"] = Environment.OSVersion.Platform.ToString(),
            ["release"] = Environment.OSVersion.Version.ToString(),
            ["version"] = Environment.Version.ToString(),
            ["machine"] = Environment.MachineName,
            ["model_name"] = model.APIId,
            ["supports_function_call"] = model.SupportFunctionCall,
            ["supports_system_prompt"] = model.SupportSystemPrompt
        };

        foreach (var pair in TemplateVariables)
        {
            variables[pair.Key] = pair.Value;
        }

        await Task.CompletedTask;
        return variables;
    }

    protected virtual bool ShouldRenderToolInstructions()
    {
        if (!IncludeToolInstructions)
        {
            return false;
        }

        return UseStructuredToolInstructionsForPlatform();
    }

    protected virtual bool ShouldRenderRagInstructions()
    {
        if (!IncludeRagInstructions)
        {
            return false;
        }

        return UseStructuredToolInstructionsForPlatform();
    }

    protected virtual bool UseStructuredToolInstructionsForPlatform()
    {
        var platform = PlatformId?.Trim().ToLowerInvariant();
        return platform == "windows";
    }

    protected virtual string BuildPlatformInstructions()
    {
        var platform = PlatformId?.Trim().ToLowerInvariant();
        return platform switch
        {
            "windows" => BuildWindowsPlatformInstructions(),
            "linux" => BuildLinuxPlatformInstructions(),
            _ => BuildGenericPlatformInstructions()
        };
    }

    protected virtual string BuildWindowsPlatformInstructions()
    {
        return
            """
            <platform_instructions platform="windows">
            You are working in a Windows-oriented development environment.

            Windows command guidance:
            - Prefer PowerShell syntax and Windows-native commands.
            - Do not assume bash, sed, awk, grep, ls, cat, or nl are available.
            - Use CMD only when PowerShell is not appropriate.
            - Commands may run in isolated processes, so do not assume shell state persists across calls.
            - If a command depends on a directory, specify it explicitly.

            File and path guidance:
            - Be careful with spaces in Windows paths and quote them when necessary.
            - Prefer structured file tools for reading and editing files.
            - Use line-numbered file inspection when precise code review is needed.
            - Do not include line number prefixes in file edit operations.

            Development workflow guidance:
            - For .NET projects, prefer commands such as dotnet build, dotnet test, or other project-specific verification commands.
            - For WPF and Avalonia projects, prefer structured file inspection and precise edits rather than shell-based text rewriting.
            - For C++ and Qt projects on Windows, use the actual project tooling and build scripts available in the repository.
            - Prefer non-interactive commands.

            Safety guidance:
            - Preview edits before applying them when possible.
            - File edits may require user confirmation through a visual diff UI.
            </platform_instructions>
            """;
    }

    protected virtual string BuildLinuxPlatformInstructions()
    {
        return
            """
            <platform_instructions platform="linux">
            You are working in a Linux development environment.

            Command guidance:
            - Prefer bash-compatible commands and standard Unix tooling when appropriate.
            - Commands may run in isolated subshells, so do not assume state persists across calls unless explicitly managed.

            File guidance:
            - Read files in focused regions when possible.
            - Prefer precise, minimal edits.

            Verification guidance:
            - Verify fixes with repository-appropriate build or test commands whenever possible.
            </platform_instructions>
            """;
    }

    protected virtual string BuildGenericPlatformInstructions()
    {
        return
            """
            <platform_instructions>
            You are operating in a development environment where commands may run in isolated processes.
            Prefer structured file tools for inspection and editing when available.
            Use non-interactive commands where possible.
            </platform_instructions>
            """;
    }

    protected virtual string BuildToolSelectionGuidance()
    {
        var platform = PlatformId?.Trim().ToLowerInvariant();
        return platform switch
        {
            "windows" => BuildWindowsToolSelectionGuidance(),
            _ => string.Empty
        };
    }

    protected virtual string BuildWindowsToolSelectionGuidance()
    {
        return
            """
            ## Tool Selection Guidance

            Prefer FileSystem tools for:
            - reading files
            - inspecting code with line numbers
            - finding text in files
            - previewing edits
            - applying edits

            Prefer WinCLI for:
            - dotnet build
            - dotnet test
            - msbuild
            - powershell scripts
            - project searches or tooling commands
            - environment inspection
            """;
    }

    protected virtual string BuildToolInstructions()
    {
        if (FunctionGroups == null)
        {
            return string.Empty;
        }

        var availableGroups = FunctionGroups
            .Where(group => group.IsAvailable)
            .Where(group => group.AvailableTools is { Count: > 0 })
            .ToList();

        if (availableGroups.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<tool_instructions>");

        foreach (var functionGroup in availableGroups)
        {
            sb.AppendLine($"""<tool name="{functionGroup.Name}">""");

            if (!string.IsNullOrWhiteSpace(functionGroup.AdditionPrompt))
            {
                sb.AppendLine(functionGroup.AdditionPrompt);
            }
            else
            {
                sb.AppendLine("This tool group is available.");
            }

            sb.AppendLine("</tool>");
        }

        sb.AppendLine("</tool_instructions>");
        return sb.ToString().TrimEnd();
    }

    protected virtual string BuildRagInstructions()
    {
        if (RagSources == null)
        {
            return string.Empty;
        }

        var availableSources = RagSources
            .Where(source => source.IsAvailable)
            .Where(source => source.AvailableTools is { Count: > 0 })
            .ToList();

        if (availableSources.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<rag_instructions>");

        foreach (var source in availableSources)
        {
            sb.AppendLine($"""<rag_source name="{source.Name}">""");

            if (!string.IsNullOrWhiteSpace(source.AdditionPrompt))
            {
                sb.AppendLine(source.AdditionPrompt);
            }
            else
            {
                sb.AppendLine("This RAG source is available.");
            }

            sb.AppendLine("</rag_source>");
        }

        sb.AppendLine("</rag_instructions>");
        return sb.ToString().TrimEnd();
    }

    protected virtual async Task<string> RenderSystemTemplateAsync(Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(SystemTemplate))
        {
            return string.Empty;
        }

        return await PromptTemplateRenderer.RenderHandlebarsAsync(SystemTemplate, variables);
    }

    protected virtual async Task<string> RenderInstanceTemplateAsync(Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(InstanceTemplate))
        {
            return string.Empty;
        }

        return await PromptTemplateRenderer.RenderHandlebarsAsync(InstanceTemplate, variables);
    }
}

public static class AgentDialogContextFactory
{
    public static AgentDialogContext Create(
        IReadOnlyList<IChatHistoryItem> history,
        MiniSweAgentConfig config,
        IChatRequest request)
    {
        var context = new AgentDialogContext(history)
        {
            PlatformId = config.PlatformId,
            SystemTemplate = config.SystemTemplate,
            InstanceTemplate = config.InstanceTemplate,
            IncludeToolInstructions = config.IncludeToolInstructions,
            IncludeRagInstructions = config.IncludeRagInstructions,
            IncludeHistoryMessages = true
        };

        context.MapFromRequest(request);
        return context;
    }
}
