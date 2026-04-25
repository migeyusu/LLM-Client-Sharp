namespace LLMClient.Agent.MiniSWE;

public enum RunPlatform
{
    Windows,

    Linux,
    
    Wsl
}

/// <summary>
/// Mini-SWE-Agent / Windows-Agent unified config
/// </summary>
public class MiniSweAgentConfig
{
    /// <summary>
    /// Platform identifier, e.g. linux / windows / wsl
    /// </summary>
    public RunPlatform PlatformId { get; set; } = RunPlatform.Windows;

    /// <summary>
    /// System message template
    /// </summary>
    public string SystemTemplate { get; set; } = """
                                                 You are a helpful assistant that can interact with a computer.
                                                 """;

    /// <summary>
    /// Instance message template
    /// </summary>
    public string InstanceTemplate { get; set; } = """
                                                   Please solve this issue: {{task}}
                                                   """;

    /// <summary>
    /// Observation result template
    /// </summary>
    public string ObservationTemplate { get; set; } = """
                                                      {{#if output.exception_info}}
                                                      <exception>{{output.exception_info}}</exception>
                                                      {{/if}}
                                                      <returncode>{{output.returncode}}</returncode>
                                                      {{#if output.output}}
                                                      <output>
                                                      {{output.output}}
                                                      </output>
                                                      {{/if}}
                                                      """;

    /// <summary>
    /// Format / tool usage error feedback template
    /// </summary>
    public string FormatErrorTemplate { get; set; } = """
                                                      Tool usage error:

                                                      <error>
                                                      {{error}}
                                                      </error>

                                                      General guidance:
                                                      - Use the available tools directly when you need to inspect files, edit files, or run commands.
                                                      - Do not describe an action without actually calling the relevant tool.
                                                      - Prefer FileSystem tools for reading and editing files.
                                                      - Prefer WinCLI for Windows command execution.
                                                      - When editing files, inspect first, preview edits, then apply edits.

                                                      If the task is complete, clearly explain what was changed and how it was verified.

                                                      """;

    /// <summary>
    /// Max steps, 0 means unlimited
    /// </summary>
    public int StepLimit { get; set; } = 0;

    /// <summary>
    /// Whether ToolCall mode is used
    /// </summary>
    public bool UseToolCall { get; set; } = true;

    /// <summary>
    /// Tool name in tool-call or text protocol
    /// </summary>
    public string ToolName { get; set; } = "bash";

    /// <summary>
    /// Execution timeout in seconds
    /// </summary>
    public int ExecutionTimeout { get; set; } = 30;

    /// <summary>
    /// Task completion flag
    /// </summary>
    public string TaskCompleteFlag { get; set; } = "COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT";

    /// <summary>
    /// Whether structured tool instructions should be injected
    /// </summary>
    public bool IncludeToolInstructions { get; set; } = true;

    /// <summary>
    /// Whether structured RAG instructions should be injected
    /// </summary>
    public bool IncludeRagInstructions { get; set; } = true;

    /// <summary>
    /// WSL distribution name. Empty means using the default distribution.
    /// </summary>
    public string WslDistributionName { get; set; } = string.Empty;

    /// <summary>
    /// WSL user name. Empty means using the distro default user.
    /// </summary>
    public string WslUserName { get; set; } = string.Empty;

    /// <summary>
    /// Whether project working directory should be mapped to /mnt/... automatically.
    /// </summary>
    public bool MapWorkingDirectoryToWsl { get; set; } = true;
}