using System.Diagnostics;
using System.Text;
using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Agent-oriented dialog context that owns prompt construction.
/// Unlike the default chat context, this context renders a dedicated
/// agent system template and instance template, and treats tools/RAG/platform
/// information as prompt materials rather than directly appending them in chat style.
/// </summary>
public class AgentRequestContextBuilder : DefaultRequestContextBuilder
{
    protected AgentRequestContextBuilder(IReadOnlyList<IChatHistoryItem> dialogItems) : base(dialogItems)
    {
    }

    public static AgentRequestContextBuilder CreateFromSession(IDialogSession session,
        MiniSweAgentConfig config)
    {
        var history = session.GetChatHistory().ToArray();
        var systemPrompt = session.SystemPrompt;
        var requestViewItem = history.LastOrDefault() as IRequestItem ??
                              throw new InvalidOperationException("RequestViewItem is null");
        var dialogContext = new AgentRequestContextBuilder(history)
        {
            DialogId = session.WorkingResponse.Id,
            SystemPrompt = systemPrompt,
            SessionId = session.ID,
            PlatformId = session is IProject projectSession
                ? projectSession.Platform
                : RunPlatform.Windows,
            ContextProviders = session.ContextProviders,
            IncludeHistoryMessages = true,
            IncludeToolInstructions = config.IncludeToolInstructions,
            IncludeRagInstructions = config.IncludeRagInstructions,
            SystemTemplate = config.SystemTemplate,
            InstanceTemplate = config.InstanceTemplate,
            WorkingDirectory = session is IProject ps
                ? ps.WorkingDirectory
                : null,
        };
        dialogContext.MapFromRequest(requestViewItem);
        return dialogContext;
    }

    public required string SystemTemplate { get; init; }

    public required string InstanceTemplate { get; init; }

    public bool IncludeHistoryMessages { get; init; } = true;

    /// <summary>
    /// 优先使用project information
    /// </summary>
    public string? ProjectInformation { get; set; }

    public RunPlatform PlatformId { get; init; } = RunPlatform.Windows;

    public bool IncludeToolInstructions { get; init; } = true;

    public bool IncludeRagInstructions { get; init; } = true;

    private readonly Dictionary<string, object?> _templateVariables = new();

    protected override string? BuildSystemPrompt()
    {
        return null;
    }

    protected override void AppendFunctionGroupsPrompt(StringBuilder builder)
    {
    }

    protected override void AppendRagSourcesPrompt(StringBuilder builder)
    {
    }

    protected override async Task<List<ChatMessage>> BuildChatHistoryAsync(IEndpointModel model,
        FunctionCallEngine functionCallEngine,
        CancellationToken cancellationToken)
    {
        var chatHistory = new List<ChatMessage>();

        var historyMessages = IncludeHistoryMessages
            ? (await GetMessagesAsync(cancellationToken)).SkipLast(1).ToList()
            : new List<ChatMessage>();

        var templateVariables = BuildTemplateVariablesAsync();
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
                Trace.TraceWarning(
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

    protected virtual Dictionary<string, object?> BuildTemplateVariablesAsync()
    {
        //优先使用project information
        var context = string.IsNullOrEmpty(ProjectInformation)
            ? $"<context>\r\n{SystemPrompt}\r\nCurrent Folder: {WorkingDirectory}\r\n</context>"
            : ProjectInformation;
        var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["task"] = UserPrompt ?? string.Empty,
            ["platform_id"] = PlatformId.ToString().ToLowerInvariant(),
            ["platform_instructions"] = BuildPlatformInstructions(),
            ["project_information"] = context,
            ["tool_instructions"] = ShouldRenderToolInstructions() ? BuildToolInstructions() : string.Empty,
            ["rag_instructions"] = ShouldRenderRagInstructions() ? BuildRagInstructions() : string.Empty,
            ["tool_selection_guidance"] = BuildToolSelectionGuidance(),
            ["working_directory"] = WorkingDirectory ?? string.Empty,
            ["system"] = Environment.OSVersion.Platform.ToString(),
            ["release"] = Environment.OSVersion.Version.ToString(),
            ["version"] = Environment.Version.ToString(),
            ["machine"] = Environment.MachineName,
        };

        foreach (var pair in _templateVariables)
        {
            variables[pair.Key] = pair.Value;
        }

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
        return PlatformId is RunPlatform.Windows or RunPlatform.Wsl;
    }

    protected virtual string BuildPlatformInstructions()
    {
        return PlatformId switch
        {
            RunPlatform.Windows => BuildWindowsPlatformInstructions(),
            RunPlatform.Wsl => BuildWslPlatformInstructions(),
            RunPlatform.Linux => BuildLinuxPlatformInstructions(),
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

    protected virtual string BuildWslPlatformInstructions()
    {
        return
            """
            <platform_instructions platform="wsl">
            You are working from a Windows host, but shell commands run inside WSL Linux.

            WSL command guidance:
            - Prefer bash-compatible Linux commands and standard Unix tooling.
            - Do not use PowerShell or CMD syntax when using the WslCLI tool.
            - Commands may run in isolated processes, so do not assume shell state persists across calls.
            - If a command depends on a directory, specify it explicitly.

            File and path guidance:
            - The project files are typically located on the Windows filesystem.
            - Linux shell commands may need paths such as /mnt/c/... instead of C:\...
            - Prefer structured file tools for reading and editing files.
            - Use line-numbered file inspection when precise code review is needed.
            - Do not include line number prefixes in file edit operations.

            Development workflow guidance:
            - Prefer non-interactive Linux commands.
            - For .NET projects, dotnet build and dotnet test are preferred verification commands.
            - Use repository-appropriate Linux tooling when available.

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
        return PlatformId switch
        {
            RunPlatform.Windows => BuildWindowsToolSelectionGuidance(),
            RunPlatform.Wsl => BuildWslToolSelectionGuidance(),
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

    protected virtual string BuildWslToolSelectionGuidance()
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

            Prefer WslCLI for:
            - bash commands
            - dotnet build
            - dotnet test
            - git commands
            - grep/find/sed/awk when appropriate
            - environment inspection inside WSL
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