using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using OpenAI.Images;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints;

public abstract class LlmClientBase : BaseViewModel, ILLMClient
{
    private bool _isResponding;
    public abstract string Name { get; }

    public abstract ILLMEndpoint Endpoint { get; }

    [JsonIgnore] public abstract ILLMModel Model { get; }

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

    [Experimental("SKEXP0001")]
    protected virtual IChatClient CreateChatClient()
    {
        return CreateChatCompletionService().AsChatClient();
    }

    protected abstract IChatCompletionService CreateChatCompletionService();

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var modelInfo = this.Model;
        var modelParams = this.Parameters;
        if (modelInfo.SystemPromptEnable && !string.IsNullOrWhiteSpace(modelParams.SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, modelParams.SystemPrompt));
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
    public virtual async Task<CompletedResult> SendRequest(IEnumerable<IDialogItem> dialogItems,
        IList<IAIFunctionGroup>? functionGroups = null, CancellationToken cancellationToken = default)
    {
        try
        {
            RespondingText.Clear();
            // PreResponse = "正在生成文档。。。。。";
            IsResponding = true;
            var chatHistory = new List<ChatMessage>();
            // Dictionary<string, AIFunction>? functionsDict = null;
            var kernelPluginCollection = new KernelPluginCollection();
            var requestOptions = this.CreateChatOptions(chatHistory);

            if (functionGroups != null)
            {
                foreach (var functionGroup in functionGroups)
                {
                    var availableTools = functionGroup.AvailableTools;
                    if (availableTools == null || availableTools.Count == 0)
                    {
                        continue;
                    }

                    kernelPluginCollection.AddFromFunctions(functionGroup.Name,
                        availableTools.Select((function => function.AsKernelFunction())));
                }

                var aiTools = kernelPluginCollection.SelectMany((plugin => plugin)).ToArray<AITool>();
                requestOptions.Tools = aiTools;
                // functionsDict = aiTools .ToDictionary(f => f.Name, f => f);
            }

            foreach (var dialogItem in dialogItems)
            {
                var chatMessage = await dialogItem.GetMessage();
                if (chatMessage != null)
                {
                    chatHistory.Add(chatMessage);
                }
            }

            string? errorMessage = null;
            int? latency = null;
            var chatClient = CreateChatClient();
            _stopwatch.Restart();
            var responseMessages = new List<ChatMessage>();
            var functionCallContents = new List<FunctionCallContent>();
            var preUpdates = new List<ChatResponseUpdate>();
            var response = new StringBuilder();
            var totalUsageDetails = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
            };
            ChatFinishReason? finishReason = null;
            while (errorMessage == null)
            {
                functionCallContents.Clear();
                preUpdates.Clear();
                try
                {
                    UsageDetails? loopUsageDetails = null;
                    if (this.Parameters.Streaming)
                    {
                        await foreach (var update in chatClient
                                           .GetStreamingResponseAsync(chatHistory, requestOptions, cancellationToken))
                        {
                            latency ??= (int)_stopwatch.ElapsedMilliseconds;
                            preUpdates.Add(update);
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
                        RespondingText.Add(chatResponse.Text);
                    }

                    finishReason = null;
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
                                case TextContent textContent:
                                    response.Append(textContent.Text);
                                    break;
                                case FunctionCallContent functionCallContent:
                                    functionCallContents.Add(functionCallContent);
                                    response.AppendLine("Function call: ");
                                    var value = functionCallContent.GetDebuggerString();
                                    response.AppendLine(value);
                                    RespondingText.Add("Function call: " + value);
                                    break;
                                default:
                                    Trace.TraceWarning("unsupported content: " + content.GetType().Name);
                                    RespondingText.Add(
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
                            RespondingText.Add("Function call finished, need run function calls...");
                        }
                        else if (finishReason == ChatFinishReason.Stop)
                        {
                            RespondingText.Add("Response completed without function calls.");
                            break;
                        }
                        else if (finishReason == ChatFinishReason.Length)
                        {
                            RespondingText.Add("Exceeded maximum response length.");
                        }
                        else
                        {
                            RespondingText.Add($"Unexpected finish reason: {finishReason}");
                        }
                    }

                    if (functionCallContents.Count == 0)
                    {
                        RespondingText.Add("No function calls were requested, response completed.");
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        errorMessage = "Operation was canceled.";
                        break;
                    }

                    if (functionsDict == null || functionsDict.Count == 0)
                    {
                        errorMessage =
                            $"No functions available to call. But {functionCallContents.Count} function calls were requested.";
                        break;
                    }

                    RespondingText.Add("Processing function calls...");
                    var chatMessage = new ChatMessage()
                    {
                        Role = ChatRole.Tool,
                    };
                    chatHistory.Add(chatMessage);
                    foreach (var functionCallContent in functionCallContents)
                    {
                        if (!functionsDict.TryGetValue(functionCallContent.Name, out var aiFunction))
                        {
                            errorMessage =
                                $"Function '{functionCallContent.Name}' not found, call failed. Procedure interrupted.";
                            break;
                        }

                        var invokeResult = await aiFunction.InvokeAsync(
                            new AIFunctionArguments(functionCallContent.Arguments),
                            cancellationToken);
                        chatMessage.Contents.Add(new FunctionResultContent(functionCallContent.CallId, invokeResult));
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
            }

            var duration = (int)(_stopwatch.ElapsedMilliseconds / 1000);
            var price = this.Model.PriceCalculator?.Calculate(totalUsageDetails);
            var log = RespondingText.Aggregate((string.Empty), (current, text) => current + text);
            return new CompletedResult(response.ToString(), totalUsageDetails, price)
            {
                ResponseLog = log,
                ErrorMessage = errorMessage,
                Latency = latency ?? 0,
                Duration = duration,
                FinishReason = finishReason,
                ChatMessages = responseMessages,
            };
        }
        finally
        {
            IsResponding = false;
        }
    }

    private UsageDetails? GetUsageDetailsFromAdditional(List<ChatResponseUpdate> updates)
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