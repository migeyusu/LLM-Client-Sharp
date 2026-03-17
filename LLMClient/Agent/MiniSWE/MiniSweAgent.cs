using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LLMClient.Abstraction;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Agent.MiniSWE;

/// <summary>
/// Mini-SWE-Agent 核心实现
/// 原汁原味的 ReAct 循环实现
/// </summary>
/*public class MiniSweAgent : ResponseViewItem
{
    private readonly IChatClient _chatClient;
    private readonly MiniSweAgentConfig _config;
    private readonly IReadOnlyList<IAIFunctionGroup> _toolProviders;

    /// <summary>
    /// 线性消息历史（这是 mini-swe-agent 的核心设计）
    /// </summary>
    public List<ChatMessage> Messages { get; private set; } = new();

    /// <summary>
    /// 额外的模板变量
    /// </summary>
    public Dictionary<string, object?> ExtraTemplateVars { get; } = new();

    /// <summary>
    /// 累计成本
    /// </summary>
    public double TotalCost { get; private set; }

    /// <summary>
    /// API 调用次数
    /// </summary>
    public int CallCount { get; private set; }

    public MiniSweAgent(
        IChatClient chatClient,
        MiniSweAgentConfig config,
        IReadOnlyList<IAIFunctionGroup>? toolProviders = null)
    {
        _chatClient = chatClient;
        _config = config;
        _toolProviders = toolProviders ?? Array.Empty<IAIFunctionGroup>();
    }

    /// <summary>
    /// 获取所有模板变量的合并结果
    /// 对应 Python 的 get_template_vars 方法
    /// </summary>
    public Dictionary<string, object?> GetTemplateVars(Dictionary<string, object?>? extra = null)
    {
        var result = new Dictionary<string, object?>();

        // 1. 配置变量
        result["step_limit"] = _config.StepLimit;
        result["cost_limit"] = _config.CostLimit;

        // 2. 环境变量 (系统信息)
        result["system"] = Environment.OSVersion.Platform.ToString();
        result["release"] = Environment.OSVersion.Version.ToString();
        result["version"] = Environment.OSVersion.VersionString;
        result["machine"] = Environment.MachineName;

        // 3. 运行时统计
        result["n_model_calls"] = CallCount;
        result["model_cost"] = TotalCost;

        // 4. 额外变量
        foreach (var kvp in ExtraTemplateVars)
        {
            result[kvp.Key] = kvp.Value;
        }

        // 5. 传入的额外变量（优先级最高）
        if (extra != null)
        {
            foreach (var kvp in extra)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// 渲染 Handlebars 模板
    /// </summary>
    private async Task<string> RenderTemplateAsync(string template, Dictionary<string, object?>? extraVars = null)
    {
        var variables = GetTemplateVars(extraVars);
        return await PromptTemplateRenderer.RenderHandlebarsAsync(template, variables);
    }

    /// <summary>
    /// 添加消息到历史
    /// </summary>
    public void AddMessages(params ChatMessage[] messages)
    {
        Messages.AddRange(messages);
    }

    /// <summary>
    /// 运行 Agent 主循环
    /// 对应 Python 的 run 方法
    /// </summary>
    public async Task<AgentResult> RunAsync(
        string task,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        // 初始化
        ExtraTemplateVars["task"] = task;
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            ExtraTemplateVars["cwd"] = workingDirectory;
        }

        Messages = new List<ChatMessage>();

        try
        {
            // 1. 添加系统消息
            var systemContent = await RenderTemplateAsync(_config.SystemTemplate);
            AddMessages(new ChatMessage(ChatRole.System, systemContent));

            // 2. 添加实例消息（任务描述）
            var instanceContent = await RenderTemplateAsync(_config.InstanceTemplate);
            AddMessages(new ChatMessage(ChatRole.User, instanceContent));

            // 3. 主循环
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await StepAsync(cancellationToken);
                }
                catch (InterruptAgentFlowException ex)
                {
                    AddMessages(ex.Messages.ToArray());

                    // 检查是否应该退出
                    if (Messages.LastOrDefault()?.Role == ChatRole.Assistant &&
                        Messages.Last().Text?.Contains("exit") == true)
                    {
                        break;
                    }
                }
                catch (SubmittedException ex)
                {
                    AddMessages(ex.Messages.ToArray());
                    return new AgentResult
                    {
                        ExitStatus = "Submitted",
                        Submission = ex.Submission,
                        Success = true
                    };
                }
                catch (LimitsExceededException)
                {
                    return new AgentResult
                    {
                        ExitStatus = "LimitsExceeded",
                        Success = false
                    };
                }

                // 检查是否已完成（最后一条是 assistant 的退出消息）
                var lastMsg = Messages.LastOrDefault();
                if (lastMsg?.Role == ChatRole.Assistant)
                {
                    // 检查是否有退出信号
                    // 这里可以根据实际情况判断
                }
            }
        }
        catch (OperationCanceledException)
        {
            return new AgentResult
            {
                ExitStatus = "Cancelled",
                Success = false
            };
        }
        catch (Exception ex)
        {
            // 处理未捕获的异常
            AddMessages(new ChatMessage(ChatRole.User,
                $"An error occurred: {ex.Message}\n\nStackTrace:\n{ex.StackTrace}"));
            throw;
        }

        return new AgentResult
        {
            ExitStatus = "Unknown",
            Success = false
        };
    }

    /// <summary>
    /// 单步执行：查询 LLM -> 执行动作
    /// 对应 Python 的 step 方法
    /// </summary>
    private async Task StepAsync(CancellationToken cancellationToken)
    {
        var responseMessage = await QueryAsync(cancellationToken);
        await ExecuteActionsAsync(responseMessage, cancellationToken);
    }

    /// <summary>
    /// 查询 LLM
    /// 对应 Python 的 query 方法
    /// </summary>
    private async Task<ChatMessage> QueryAsync(CancellationToken cancellationToken)
    {
        // 检查限制
        if (_config.StepLimit > 0 && CallCount >= _config.StepLimit)
        {
            throw new LimitsExceededException(
                new ChatMessage(ChatRole.Assistant, "Step limit exceeded"));
        }

        if (_config.CostLimit > 0 && TotalCost >= _config.CostLimit)
        {
            throw new LimitsExceededException(
                new ChatMessage(ChatRole.Assistant, "Cost limit exceeded"));
        }

        CallCount++;

        // 构建请求
        var chatMessages = Messages.ToList();
        List<AITool>? tools = null;

        if (_config.UseToolCall && _toolProviders.Count > 0)
        {
            tools = new List<AITool>();
            foreach (var provider in _toolProviders.Where(p => p.IsAvailable))
            {
                if (provider.AvailableTools != null)
                {
                    foreach (var tool in provider.AvailableTools)
                    {
                        tools.Add(AIFunctionFactory.CreateTool(tool));
                    }
                }
            }
        }

        // 调用 LLM
        var options = new ChatOptions
        {
            Tools = tools
        };

        var response = await _chatClient.CompleteAsync(chatMessages, options, cancellationToken);
        var responseMessage = response.Message;

        // 追加到历史
        AddMessages(responseMessage);

        return responseMessage;
    }

    /// <summary>
    /// 执行动作
    /// 对应 Python 的 execute_actions 方法
    /// </summary>
    private async Task ExecuteActionsAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        // 解析动作
        var actions = ParseActions(message);

        if (actions.Count == 0)
        {
            // 没有找到动作，发送格式错误反馈
            var errorContent = await RenderTemplateAsync(_config.FormatErrorTemplate, new Dictionary<string, object?>
            {
                ["error"] = "No action found in the response. Please provide at least one command.",
                ["actions"] = Array.Empty<string>()
            });

            throw new FormatErrorException(new ChatMessage(ChatRole.User, errorContent));
        }

        // 执行每个动作
        var outputs = new List<ExecutionOutput>();
        foreach (var action in actions)
        {
            var output = await ExecuteCommandAsync(action.Command, cancellationToken);
            outputs.Add(output);

            // 检查是否完成任务
            CheckTaskCompletion(output);
        }

        // 格式化观察消息
        var observationMessages = await FormatObservationMessagesAsync(actions, outputs);
        AddMessages(observationMessages.ToArray());
    }

    /// <summary>
    /// 解析 LLM 响应中的动作
    /// </summary>
    private List<ParsedAction> ParseActions(ChatMessage message)
    {
        var actions = new List<ParsedAction>();

        if (_config.UseToolCall && message.Contents != null)
        {
            // ToolCall 模式
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent toolCall)
                {
                    if (toolCall.Name == _config.ToolName)
                    {
                        try
                        {
                            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                                toolCall.Arguments ?? "{}");
                            if (args?.TryGetValue("command", out var cmd) == true)
                            {
                                actions.Add(new ParsedAction
                                {
                                    Command = cmd.GetString() ?? string.Empty,
                                    ToolCallId = toolCall.CallId
                                });
                            }
                        }
                        catch
                        {
                            // 解析失败，忽略
                        }
                    }
                }
            }
        }
        else
        {
            // TEXT 模式：使用正则解析（兼容原始 SWE-agent）
            var content = message.Text ?? string.Empty;
            actions = ParseTextBasedActions(content);
        }

        return actions;
    }

    /// <summary>
    /// 使用正则解析文本模式的动作
    /// 对应 Python 的 actions_text.py
    /// </summary>
    private List<ParsedAction> ParseTextBasedActions(string content)
    {
        var actions = new List<ParsedAction>();

        // 匹配 ```mswea_bash_command 或 ```bash 代码块
        var regex = new Regex(@"```(?:mswea_bash_command|bash)\s*\n(.*?)```", RegexOptions.Singleline);
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                actions.Add(new ParsedAction
                {
                    Command = match.Groups[1].Value.Trim()
                });
            }
        }

        return actions;
    }

    /// <summary>
    /// 执行命令
    /// 对应 Python 的 LocalEnvironment.execute
    /// </summary>
    private async Task<ExecutionOutput> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var workingDir = ExtraTemplateVars.TryGetValue("cwd", out var cwd)
                ? cwd?.ToString() ?? Directory.GetCurrentDirectory()
                : Directory.GetCurrentDirectory();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                    Arguments = OperatingSystem.IsWindows()
                        ? $"/c {command}"
                        : $"-c \"{command.Replace("\"", "\\\"")}\"",
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.ExecutionTimeout));

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cts.Token));

            var output = outputTask.Result;
            var error = errorTask.Result;

            return new ExecutionOutput
            {
                Output = output + error,
                ReturnCode = process.ExitCode,
                ExceptionInfo = process.ExitCode != 0 ? $"Command exited with code {process.ExitCode}" : null
            };
        }
        catch (Exception ex)
        {
            return new ExecutionOutput
            {
                Output = string.Empty,
                ReturnCode = -1,
                ExceptionInfo = $"Exception: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 检查任务是否完成
    /// 对应 Python 的 _check_finished 方法
    /// </summary>
    private void CheckTaskCompletion(ExecutionOutput output)
    {
        var lines = output.Output.TrimStart().Split('\n');
        if (lines.Length > 0 &&
            lines[0].Trim() == _config.TaskCompleteFlag &&
            output.ReturnCode == 0)
        {
            var submission = string.Join('\n', lines.Skip(1));
            throw new SubmittedException(
                submission,
                new ChatMessage(ChatRole.Assistant, submission));
        }
    }

    /// <summary>
    /// 格式化观察消息
    /// 对应 Python 的 format_observation_messages
    /// </summary>
    private async Task<List<ChatMessage>> FormatObservationMessagesAsync(
        List<ParsedAction> actions,
        List<ExecutionOutput> outputs)
    {
        var messages = new List<ChatMessage>();

        // 填充输出（确保每个 action 有对应的 output）
        var paddedOutputs = outputs.ToList();
        while (paddedOutputs.Count < actions.Count)
        {
            paddedOutputs.Add(new ExecutionOutput
            {
                Output = string.Empty,
                ReturnCode = -1,
                ExceptionInfo = "Action was not executed"
            });
        }

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var output = paddedOutputs[i];

            // 使用模板渲染观察内容
            var content = await RenderTemplateAsync(_config.ObservationTemplate, new Dictionary<string, object?>
            {
                ["output"] = output
            });

            var message = new ChatMessage(ChatRole.Tool, content);

            // 如果有 ToolCallId，设置为工具响应
            if (!string.IsNullOrEmpty(action.ToolCallId))
            {
                message = new ChatMessage(
                    ChatRole.Tool,
                    [new FunctionResultContent(action.ToolCallId, content)]);
            }

            messages.Add(message);
        }

        return messages;
    }

    /// <summary>
    /// 序列化 Agent 状态
    /// </summary>
    public AgentTrajectory Serialize()
    {
        return new AgentTrajectory
        {
            Info = new AgentInfo
            {
                ModelStats = new ModelStats
                {
                    TotalCost = TotalCost,
                    ApiCalls = CallCount
                },
                ExitStatus = Messages.LastOrDefault()?.Text?.Contains("exit") == true ? "Completed" : "Unknown",
                Submission = string.Empty
            },
            Messages = Messages.Select(m => new SerializedMessage
            {
                Role = m.Role.ToString(),
                Content = m.Text ?? string.Empty
            }).ToList()
        };
    }
}*/

/// <summary>
/// Agent 运行结果
/// </summary>
public class AgentResult
{
    public string ExitStatus { get; set; } = string.Empty;
    public string Submission { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Agent 轨迹（用于保存和调试）
/// </summary>
public class AgentTrajectory
{
    public AgentInfo Info { get; set; } = new();
    public List<SerializedMessage> Messages { get; set; } = new();
}

public class AgentInfo
{
    public ModelStats ModelStats { get; set; } = new();
    public string ExitStatus { get; set; } = string.Empty;
    public string Submission { get; set; } = string.Empty;
}

public class ModelStats
{
    public double TotalCost { get; set; }
    public int ApiCalls { get; set; }
}

public class SerializedMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}