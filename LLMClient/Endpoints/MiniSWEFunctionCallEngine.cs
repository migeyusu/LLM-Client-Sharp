using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ToolCall.DefaultPlugins;
using Microsoft.Extensions.AI;
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

    public override bool IsToolCallMode
    {
        get { return _config.UseToolCall; }
    }

    public override void PreviewRequest(ChatOptions options, IEndpointModel model, IList<ChatMessage> chatMessages)
    {
        // agent will do
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
            return await ParseTextBasedActions(textContent.Text);
        }

        return [];
    }

    private async Task<List<FunctionCallContent>> ParseTextBasedActions(string content)
    {
        var actions = new List<FunctionCallContent>();
        var regex = MyRegex();
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                actions.Add(new FunctionCallContent(string.Empty, GetExecuteCommandFunctionName(),
                    new Dictionary<string, object?>
                    {
                        ["command"] = match.Groups[1].Value.Trim()
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
                });

            throw new Exception(errorMsg);
        }

        if (actions.Count == 0)
        {
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
            return;
        }

        replyMessage.Role = ChatRole.User;
        var resultContent = results.First();
        var (success, content) = await EncapsulateRaw(resultContent);
        if (!success)
        {
            content = resultContent.ToString();
        }

        replyMessage.Contents.Add(new TextContent(content ?? string.Empty));
    }

    private async Task<(bool Success, string? Content)> EncapsulateRaw(FunctionResultContent result)
    {
        var output = result.Result?.ToString();
        if (string.IsNullOrWhiteSpace(output))
        {
            return (false, string.Empty);
        }

        ExecutionOutput? executionOutput;
        try
        {
            executionOutput = JsonSerializer.Deserialize<ExecutionOutput>(output);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            return (false, string.Empty);
        }

        if (executionOutput == null)
        {
            return (false, string.Empty);
        }

        var renderHandlebarsAsync = await PromptTemplateRenderer.RenderHandlebarsAsync(_config.ObservationTemplate,
            new Dictionary<string, object?>
            {
                ["output"] = executionOutput
            });
        return (true, renderHandlebarsAsync);
    }

    private void CheckTaskCompletion(FunctionResultContent result)
    {
        var output = result.Result?.ToString();
        if (string.IsNullOrWhiteSpace(output))
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

    private string GetExecuteCommandFunctionName()
    {
        var platformId = _config.PlatformId?.Trim().ToLowerInvariant();
        return platformId switch
        {
            MiniSwePlatforms.Wsl => "WslCLI_ExecuteCommandAsync",
            MiniSwePlatforms.Linux => "WslCLI_ExecuteCommandAsync",
            _ => "WinCLI_ExecuteCommandAsync"
        };
    }

    [GeneratedRegex(@"```(?:mswea_bash_command|bash)\s*\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex MyRegex();
}
