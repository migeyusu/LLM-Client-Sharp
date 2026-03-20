using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints;

public partial class MiniSWEFunctionCallEngine : FunctionCallEngine
{
    private readonly MiniSweAgentConfig _config;

    public MiniSWEFunctionCallEngine(MiniSweAgentConfig config)
    {
        _config = config;
    }

    public override void PreviewRequest(ChatOptions options, IEndpointModel model, IList<ChatMessage> chatMessages)
    {
        //agent will do
    }

    public override async Task<List<FunctionCallContent>> TryParseFunctionCalls(ChatResponse response)
    {
        if (_config.UseToolCall)
        {
            return ExtractFunctionCallsFromResponse(response);
        }

        var content = response.Messages.FirstOrDefault()?.Contents.FirstOrDefault();
        if (content is TextContent textContent)
        {
            // TEXT 模式：使用正则解析（兼容原始 SWE-agent）
            var functionCallContents = await ParseTextBasedActions(textContent.Text);
            return functionCallContents;
        }

        return [];
    }

    /// <summary>
    /// 使用正则解析文本模式的动作
    /// 对应 Python 的 actions_text.py
    /// </summary>
    private async Task<List<FunctionCallContent>> ParseTextBasedActions(string content)
    {
        var actions = new List<FunctionCallContent>();
        // 匹配 ```mswea_bash_command 或 ```bash 代码块
        var regex = MyRegex();
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                actions.Add(new FunctionCallContent("", "WinCLI_ExecuteCommand", new Dictionary<string, object?>
                {
                    { "command", match.Groups[1].Value.Trim() }
                }));
            }
        }

        if (actions.Count > 1)
        {
            var errorMsg = await PromptTemplateRenderer.RenderHandlebarsAsync(
                _config.FormatErrorTemplate,
                new Dictionary<string, object?>
                {
                    ["error"] = $"Expected exactly 1 action, found {matches.Count}",
                    ["actions"] = new { length = matches.Count }
                }
            );

            throw new Exception(errorMsg);
        }

        if (actions.Count == 0)
        {
            // 没有找到动作，发送格式错误反馈
            var errorContent = await PromptTemplateRenderer.RenderHandlebarsAsync(_config.FormatErrorTemplate,
                new Dictionary<string, object?>
                {
                    ["error"] = "No action found in the response. Please provide at least one command.",
                    ["actions"] = Array.Empty<string>()
                });

            throw new Exception(errorContent);
        }

        return actions;
    }

    public override async Task AfterProcess(ChatMessage replyMessage, IList<FunctionResultContent> results)
    {
        foreach (var result in results)
        {
            CheckTaskCompletion(result);
        }

        if (_config.UseToolCall)
        {
            EncapsulateReply(replyMessage, results);
        }
        else
        {
            replyMessage.Role = ChatRole.User;
            //非toolcall下每次只产生一个信息
            var resultContent = results.First();
            var (b, s) = await EncapsulateRaw(resultContent);
            if (b)
            {
                s = resultContent.ToString();
            }

            replyMessage.Contents.Add(new TextContent(s));
        }
    }


    private async Task<Tuple<bool, string?>> EncapsulateRaw(FunctionResultContent result)
    {
        var output = result.Result?.ToString();
        if (string.IsNullOrEmpty(output))
        {
            return new Tuple<bool, string?>(false, string.Empty);
        }

        ExecutionOutput? executionOutput;
        try
        {
            executionOutput = JsonSerializer.Deserialize<ExecutionOutput>(output);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            return new Tuple<bool, string?>(false, string.Empty);
        }

        if (executionOutput == null)
        {
            return new Tuple<bool, string?>(false, string.Empty);
        }
        // 使用模板渲染观察内容

        var renderHandlebarsAsync = await PromptTemplateRenderer.RenderHandlebarsAsync(_config.ObservationTemplate,
            new Dictionary<string, object?>
            {
                ["output"] = output
            });
        return new Tuple<bool, string?>(true, renderHandlebarsAsync);
    }


    /// <summary>
    /// 检查任务是否完成
    /// 对应 Python 的 _check_finished 方法
    /// </summary>
    private void CheckTaskCompletion(FunctionResultContent result)
    {
        var output = result.Result?.ToString();
        if (string.IsNullOrEmpty(output))
        {
            return;
        }

        ExecutionOutput? executionOutput;
        try
        {
            executionOutput = JsonSerializer.Deserialize<ExecutionOutput>(output);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            return;
        }

        if (executionOutput == null)
        {
            return;
        }

        var lines = executionOutput.Output.TrimStart().Split('\n');
        if (lines.Length > 0 &&
            lines[0].Trim() == _config.TaskCompleteFlag &&
            executionOutput.ReturnCode == 0)
        {
            var submission = string.Join('\n', lines.Skip(1));
            throw new SubmittedException(
                submission,
                new ChatMessage(ChatRole.Assistant, submission));
        }
    }

    [GeneratedRegex(@"```(?:mswea_bash_command|bash)\s*\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex MyRegex();
}