using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using LLMClient.UI.MCP;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints;

public abstract class LlmClientBase : BaseViewModel, ILLMClient
{
    public abstract string Name { get; }

    public abstract ILLMEndpoint Endpoint { get; }

    [JsonIgnore] public abstract ILLMModel Model { get; }

    public bool IsQuitWhenFunctionCallFailed { get; set; } = true;

    private bool _isResponding;

    public bool IsResponding
    {
        get => _isResponding;
        set
        {
            if (value == _isResponding) return;
            _isResponding = value;
            OnPropertyChanged();
        }
    }

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    public IFunctionInterceptor FunctionInterceptor { get; set; } = FunctionAuthorizationInterceptor.Instance;

    [JsonIgnore]
    public virtual ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    protected abstract IChatClient GetChatClient();

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages, IList<AITool>? tools = null,
        string? systemPrompt = null)
    {
        var modelInfo = this.Model;
        var modelParams = this.Parameters;
        if (modelInfo.SystemPromptEnable && !string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        var chatOptions = new ChatOptions()
        {
            ModelId = modelInfo.Id,
        };

        if (modelInfo.TopPEnable)
        {
            chatOptions.TopP = modelParams.TopP;
        }

        if (modelInfo.TopKEnable)
        {
            chatOptions.TopK = modelParams.TopK;
        }

        if (modelInfo.TemperatureEnable)
        {
            chatOptions.Temperature = modelParams.Temperature;
        }

        if (modelInfo.MaxTokensEnable)
        {
            chatOptions.MaxOutputTokens = modelParams.MaxTokens;
        }

        if (modelInfo.FrequencyPenaltyEnable)
        {
            chatOptions.FrequencyPenalty = modelParams.FrequencyPenalty;
        }

        if (modelInfo.PresencePenaltyEnable)
        {
            chatOptions.PresencePenalty = modelParams.PresencePenalty;
        }

        if (modelInfo.SeedEnable && modelParams.Seed.HasValue)
        {
            chatOptions.Seed = modelParams.Seed.Value;
        }

        if (tools != null)
        {
            if (this.Model.SupportFunctionCall)
            {
                chatOptions.Tools = tools;
            }
            else
            {
                throw new NotSupportedException(
                    "This model does not support function calls, but tools were provided.");
            }
        }

        return chatOptions;
    }


    private readonly Stopwatch _stopwatch = new Stopwatch();

    [Experimental("SKEXP0001")]
    public virtual async Task<CompletedResult> SendRequest(DialogContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            RespondingText.Clear();
            IsResponding = true;
            var dialogItems = context.DialogItems;
            var systemPrompt = context.SystemPrompt;
            var requestViewItem = dialogItems.LastOrDefault() as RequestViewItem;
            var chatHistory = new List<ChatMessage>();
            AITool[]? tools = null;
            var kernelPluginCollection = new KernelPluginCollection();
            var toolsPromptBuilder = new StringBuilder();
            var functionGroups = requestViewItem?.FunctionGroups;
            if (functionGroups != null && functionGroups.Any())
            {
                foreach (var functionGroup in functionGroups)
                {
                    await functionGroup.EnsureAsync(cancellationToken);
                    var availableTools = functionGroup.AvailableTools;
                    if (availableTools == null || availableTools.Count == 0)
                    {
                        continue;
                    }

                    toolsPromptBuilder.AppendLine(functionGroup.AdditionPrompt);
                    kernelPluginCollection.AddFromFunctions(functionGroup.Name,
                        availableTools.Select((function => function.AsKernelFunction())));
                }

                tools = kernelPluginCollection.SelectMany((plugin => plugin)).ToArray<AITool>();
                if (!tools.Any())
                {
                    Trace.TraceWarning("No available tools found in function groups.");
                }
            }

            if (toolsPromptBuilder.Length > 0)
            {
                systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? toolsPromptBuilder.ToString()
                    : $"{systemPrompt}\n{toolsPromptBuilder}";
            }
            else if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                systemPrompt = "No tools available.";
            }

            var requestOptions = this.CreateChatOptions(chatHistory, tools, systemPrompt);
            foreach (var dialogItem in dialogItems)
            {
                await foreach (var message in dialogItem.GetMessages(cancellationToken))
                {
                    chatHistory.Add(message);
                }
            }

            string? errorMessage = null;
            int? latency = null;
            var chatClient = GetChatClient();
            _stopwatch.Restart();
            //本次响应的消息列表
            var responseMessages = new List<ChatMessage>();
            var preFunctionCalls = new List<FunctionCallContent>();
            var preUpdates = new List<ChatResponseUpdate>();
            var totalUsageDetails = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
            };
            ChatFinishReason? finishReason = null;
            while (true)
            {
                preFunctionCalls.Clear();
                preUpdates.Clear();
                UsageDetails? loopUsageDetails = null;
                try
                {
                    ChatResponse? preResponse = null;
                    if (this.Parameters.Streaming)
                    {
                        await foreach (var update in chatClient
                                           .GetStreamingResponseAsync(chatHistory, requestOptions, cancellationToken))
                        {
                            latency ??= (int)_stopwatch.ElapsedMilliseconds;
                            preUpdates.Add(update);
                            //只收集文本内容
                            RespondingText.Add(update.Text);
                        }

                        preResponse = preUpdates.ToChatResponse();
                    }
                    else
                    {
                        preResponse = await chatClient.GetResponseAsync(chatHistory, requestOptions, cancellationToken);
                        //只收集文本内容
                        RespondingText.Add(preResponse.Text);
                    }

                    var preResponseMessages = preResponse.Messages;
                    chatHistory.AddRange(preResponseMessages);
                    responseMessages.AddRange(preResponseMessages);
                    foreach (var preResponseMessage in preResponseMessages)
                    {
                        foreach (var content in preResponseMessage.Contents)
                        {
                            switch (content)
                            {
                                case UsageContent usageContent:
                                    (loopUsageDetails ??= new UsageDetails()).Add(usageContent.Details);
                                    break;
                                case TextContent:
                                    //do nothing, textContent is already added to RespondingText
                                    break;
                                case FunctionCallContent functionCallContent:
                                    preFunctionCalls.Add(functionCallContent);
                                    RespondingText.NewLine(functionCallContent.GetDebuggerString());
                                    break;
                                case FunctionResultContent functionResultContent:
                                    RespondingText.NewLine(functionResultContent.GetDebuggerString());
                                    break;
                                default:
                                    Trace.TraceWarning("unsupported content: " + content.GetType().Name);
                                    RespondingText.NewLine(
                                        $"Unsupported content type: {content.GetType().Name}");
                                    RespondingText.Add(content.RawRepresentation?.ToString() ?? string.Empty);
                                    break;
                            }
                        }
                    }

                    loopUsageDetails ??= GetUsageDetailsFromAdditional(preUpdates);
                    if (loopUsageDetails != null)
                    {
                        totalUsageDetails.Add(loopUsageDetails);
                    }

                    finishReason = preResponse.FinishReason;
                    if (finishReason == null)
                    {
                        //do nothing
                    }
                    else if (finishReason == ChatFinishReason.ToolCalls)
                    {
                        Trace.TraceInformation("Function call finished, need run function calls...");
                    }
                    else if (finishReason == ChatFinishReason.Stop)
                    {
                        Trace.TraceInformation("Response completed without function calls.");
                        break;
                    }
                    else if (finishReason == ChatFinishReason.Length)
                    {
                        Trace.TraceInformation("Exceeded maximum response length.");
                        break;
                    }
                    else if (finishReason == ChatFinishReason.ContentFilter)
                    {
                        Trace.TraceWarning("Response was filtered by content filter.");
                        break;
                    }
                    else
                    {
                        Trace.TraceWarning($"Unexpected finish reason: {finishReason}");
                    }

                    if (preFunctionCalls.Count == 0)
                    {
                        Trace.TraceInformation("No function calls were requested, response completed.");
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        errorMessage = "Operation was canceled.";
                        break;
                    }

                    if (kernelPluginCollection.Count == 0)
                    {
                        errorMessage =
                            $"No functions available to call. But {preFunctionCalls.Count} function calls were requested.";
                        break;
                    }

                    RespondingText.NewLine("Processing function calls...");
                    var chatMessage = new ChatMessage() { Role = ChatRole.Tool, };
                    chatHistory.Add(chatMessage);
                    responseMessages.Add(chatMessage);
                    foreach (var functionCallContent in preFunctionCalls)
                    {
                        if (!kernelPluginCollection.TryGetFunction(null, functionCallContent.Name,
                                out var kernelFunction))
                        {
                            errorMessage =
                                $"Function '{functionCallContent.Name}' not found, call failed. Procedure interrupted.";
                            break;
                        }

                        try
                        {
                            var arguments = new AIFunctionArguments(functionCallContent.Arguments);
                            //调用拦截器
                            var invokeResult = await this.FunctionInterceptor.InvokeAsync(
                                kernelFunction, arguments, functionCallContent, cancellationToken);
                            RespondingText.NewLine(
                                $"Function '{functionCallContent.Name}' invoked successfully, result: {invokeResult}");
                            chatMessage.Contents.Add(
                                new FunctionResultContent(functionCallContent.CallId, invokeResult));
                        }
                        catch (Exception e)
                        {
                            RespondingText.NewLine("Function call failed: " + e.Message);
                            chatMessage.Contents.Add(new FunctionResultContent(functionCallContent.CallId, null)
                                { Exception = e });
                            if (IsQuitWhenFunctionCallFailed)
                            {
                                errorMessage = $"Function '{functionCallContent.Name}' invocation failed: {e.Message}";
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    errorMessage = "Operation was canceled.";
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    Trace.TraceError($"Error during response: {ex}");
                }

                if (errorMessage != null)
                {
                    break;
                }

                RespondingText.NewLine();
            }

            var duration = (int)(_stopwatch.ElapsedMilliseconds / 1000);
            var price = this.Model.PriceCalculator?.Calculate(totalUsageDetails);
            return new CompletedResult(totalUsageDetails, responseMessages)
            {
                ErrorMessage = errorMessage,
                Latency = latency ?? 0,
                Duration = duration,
                FinishReason = finishReason,
                Price = price,
            };
        }
        finally
        {
            IsResponding = false;
        }
    }

    private static UsageDetails? GetUsageDetailsFromAdditional(IList<ChatResponseUpdate> updates)
    {
        UsageDetails? usageDetails = null;
        foreach (var update in updates)
        {
            var additionalProperties = update.AdditionalProperties;
            if (additionalProperties != null)
            {
                // 方法1: 检查 Metadata 中的 Usage 信息
                if (additionalProperties.TryGetValue("Usage", out var usageObj))
                {
                    if (usageObj is ChatTokenUsage chatTokenUsage)
                    {
                        var details = new UsageDetails()
                        {
                            InputTokenCount = chatTokenUsage.InputTokenCount,
                            OutputTokenCount = chatTokenUsage.OutputTokenCount,
                            TotalTokenCount = chatTokenUsage.TotalTokenCount,
                        };
                        if (usageDetails == null)
                        {
                            usageDetails = details;
                        }
                        else
                        {
                            usageDetails.Add(details);
                        }
                    }

                    /*if (usage.TryGetValue("TotalTokens", out var totalTokensObj))
                    {
                        // tokenCount = Convert.ToInt32(totalTokensObj);
                    }*/
                }
                else
                {
                    Debugger.Break();
                }
                // 方法2: 部分 AI 服务可能使用不同的元数据键
                /*if (usageDetails == null &&
                    dictionary.TryGetValue("CompletionTokenCount", out var completionTokensObj))
                {
                    // tokenCount = Convert.ToInt32(completionTokensObj);
                }

                // 方法3: 有些版本可能在 ModelResult 中提供 usage
                if (usageDetails == null &&
                    dictionary.TryGetValue("ModelResults", out var modelResultsObj))
                {
                    //do what?
                }*/
            }
        }


        return usageDetails;
    }
}