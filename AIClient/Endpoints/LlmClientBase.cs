using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Dialog;
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

    [JsonIgnore]
    public virtual ObservableCollection<string> RespondingText { get; } = new ObservableCollection<string>();

    protected abstract IChatClient GetChatClient();

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages, string? systemPrompt = null)
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

        return chatOptions;
    }

    private readonly Stopwatch _stopwatch = new Stopwatch();

    [Experimental("SKEXP0001")]
    public virtual async Task<CompletedResult> SendRequest(IList<IDialogItem> dialogItems,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            RespondingText.Clear();
            IsResponding = true;
            var chatHistory = new List<ChatMessage>();
            var kernelPluginCollection = new KernelPluginCollection();
            var requestOptions = this.CreateChatOptions(chatHistory, systemPrompt);
            if (dialogItems.LastOrDefault() is not RequestViewItem requestViewItem)
            {
                throw new NotSupportedException("RequestViewItem is required in dialog items.");
            }

            if (requestViewItem.FunctionGroups != null)
            {
                foreach (var functionGroup in requestViewItem.FunctionGroups)
                {
                    var availableTools = await functionGroup.GetToolsAsync(cancellationToken);
                    if (availableTools.Count == 0)
                    {
                        continue;
                    }

                    kernelPluginCollection.AddFromFunctions(functionGroup.Name,
                        availableTools.Select((function => function.AsKernelFunction())));
                }

                var aiTools = kernelPluginCollection.SelectMany((plugin => plugin)).ToArray<AITool>();
                if (aiTools.Length > 0)
                {
                    if (!this.Model.SupportFunctionCall)
                    {
                        throw new NotSupportedException("Function call is not supported.");
                    }

                    requestOptions.Tools = aiTools;
                }
                else
                {
                    Trace.TraceWarning("No available tools found in function groups.");
                }
            }

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
            var responseMessages = new List<ChatMessage>();
            var functionCallContents = new List<FunctionCallContent>();
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
                functionCallContents.Clear();
                preUpdates.Clear();
                finishReason = null;
                UsageDetails? loopUsageDetails = null;
                try
                {
                    if (this.Parameters.Streaming)
                    {
                        await foreach (var update in chatClient
                                           .GetStreamingResponseAsync(chatHistory, requestOptions, cancellationToken))
                        {
                            latency ??= (int)_stopwatch.ElapsedMilliseconds;
                            preUpdates.Add(update);
                            //只收集文本内容
                            RespondingText.Add(update.Text);
                            var message = new ChatMessage(update.Role ?? ChatRole.Assistant, update.Contents)
                            {
                                AdditionalProperties = update.AdditionalProperties,
                                AuthorName = update.AuthorName,
                                RawRepresentation = update.RawRepresentation,
                                MessageId = update.MessageId,
                            };
                            responseMessages.Add(message);
                            chatHistory.Add(message);
                        }
                    }
                    else
                    {
                        var chatResponse =
                            await chatClient.GetResponseAsync(chatHistory, requestOptions, cancellationToken);
                        var details = chatResponse.Usage;
                        if (loopUsageDetails == null)
                        {
                            loopUsageDetails = details;
                        }
                        else if (details != null)
                        {
                            loopUsageDetails.Add(details);
                        }

                        var chatMessages = chatResponse.Messages;
                        chatHistory.AddRange(chatMessages);
                        responseMessages.AddRange(chatMessages);
                        preUpdates.AddRange(chatResponse.ToChatResponseUpdates());
                        //只收集文本内容
                        RespondingText.Add(chatResponse.Text);
                    }

                    foreach (var update in preUpdates)
                    {
                        finishReason ??= update.FinishReason;
                        foreach (var content in update.Contents)
                        {
                            switch (content)
                            {
                                case UsageContent usageContent:
                                    var usage = usageContent.Details;
                                    if (loopUsageDetails == null)
                                    {
                                        loopUsageDetails = usage;
                                    }
                                    else
                                    {
                                        loopUsageDetails.Add(usage);
                                    }

                                    break;
                                case TextContent:
                                    //do nothing, textContent is already added to RespondingText
                                    break;
                                case FunctionCallContent functionCallContent:
                                    functionCallContents.Add(functionCallContent);
                                    var value = functionCallContent.GetDebuggerString();
                                    RespondingText.NewLine("Function call: " + value);
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

                    if (finishReason != null)
                    {
                        if (finishReason == ChatFinishReason.ToolCalls)
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
                    }

                    if (functionCallContents.Count == 0)
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
                            $"No functions available to call. But {functionCallContents.Count} function calls were requested.";
                        break;
                    }

                    RespondingText.NewLine("Processing function calls...");
                    var chatMessage = new ChatMessage() { Role = ChatRole.Tool, };
                    chatHistory.Add(chatMessage);
                    responseMessages.Add(chatMessage);
                    foreach (var functionCallContent in functionCallContents)
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
                            var invokeResult = await kernelFunction.InvokeAsync(
                                new AIFunctionArguments(functionCallContent.Arguments),
                                cancellationToken);
                            RespondingText.NewLine(
                                $"Function '{functionCallContent.Name}' invoked successfully, result: {invokeResult}");
                            chatMessage.Contents.Add(
                                new FunctionResultContent(functionCallContent.CallId, invokeResult));
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError($"Function call error: {e}");
                            RespondingText.NewLine("Function call failed: " + e.Message);
                            chatMessage.Contents.Add(new FunctionResultContent(functionCallContent.CallId, null)
                                { Exception = e });
                            if (this.IsQuitWhenFunctionCallFailed)
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

    private static UsageDetails? GetUsageDetailsFromAdditional(List<ChatResponseUpdate> updates)
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