namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent 配置
/// 对应 Python 版本的 AgentConfig
/// </summary>
public class MiniSweAgentConfig
{
    /// <summary>
    /// 系统消息模板（第一条消息）
    /// 使用 Handlebars 语法，变量如 {{system}}, {{release}} 等
    /// </summary>
    public string SystemTemplate { get; set; } = """
                                                 You are a helpful assistant that can interact with a computer.
                                                 """;

    /// <summary>
    /// 实例模板（第二条消息，指定任务）
    /// 使用 Handlebars 语法，变量如 {{task}}
    /// </summary>
    public string InstanceTemplate { get; set; } = """
                                                   Please solve this issue: {{task}}

                                                   You can execute bash commands and edit files to implement the necessary changes.
                                                   """;

    /// <summary>
    /// 观察结果模板（执行命令后的输出格式）
    /// 使用 Handlebars 语法
    /// </summary>
    public string ObservationTemplate { get; set; } = """
                                                      {{"{{"}}#if output.exception_info{{"}}"}}
                                                      <exception>{{"{{"}}output.exception_info{{"}}"}}</exception>
                                                      {{"{{"}}/if{{"}}"}}
                                                      <returncode>{{"{{"}}output.returncode{{"}}"}}</returncode>
                                                      <output>
                                                      {{"{{"}}output.output{{"}}"}}
                                                      </output>
                                                      """;

    /// <summary>
    /// 格式错误反馈模板
    /// </summary>
    public string FormatErrorTemplate { get; set; } = """
                                                      Format error:

                                                      <error>
                                                      {{"{{"}}error{{"}}"}}
                                                      </error>

                                                      Please format your response correctly.
                                                      """;

    /// <summary>
    /// 最大步数限制（0 表示无限制）
    /// </summary>
    public int StepLimit { get; set; } = 0;

    /// <summary>
    /// 是否使用 ToolCall 模式（否则使用正则解析）
    /// </summary>
    public bool UseToolCall { get; set; } = true;

    /// <summary>
    /// Tool 名称（当 UseToolCall=true 时）
    /// </summary>
    public string ToolName { get; set; } = "bash";

    /// <summary>
    /// 命令执行超时时间（秒）
    /// </summary>
    public int ExecutionTimeout { get; set; } = 30;

    /// <summary>
    /// 任务完成标志字符串
    /// </summary>
    public string TaskCompleteFlag { get; set; } = "COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT";
    
}


/// <summary>
/// 解析出的 Action
/// </summary>
public class ParsedAction
{
    public string Command { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }
}