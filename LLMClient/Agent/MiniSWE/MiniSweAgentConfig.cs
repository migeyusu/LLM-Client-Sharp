namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent / Windows-Agent unified config
/// </summary>
public class MiniSweAgentConfig
{
    /// <summary>
    /// Platform identifier, e.g. linux / windows
    /// </summary>
    public string PlatformId { get; set; } = "linux";

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
                                                      <returncode>{{output.returncode}}</returncode>
                                                      <output>
                                                      {{output.output}}
                                                      </output>
                                                      """;

    /// <summary>
    /// Format / tool usage error feedback template
    /// </summary>
    public string FormatErrorTemplate { get; set; } = """
                                                      Format error:

                                                      <error>
                                                      {{error}}
                                                      </error>
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
}

/// <summary>
/// Parsed action
/// </summary>
public class ParsedAction
{
    public string Command { get; set; } = string.Empty;

    public string? ToolCallId { get; set; }
}