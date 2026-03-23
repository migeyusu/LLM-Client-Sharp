using System.ClientModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Rag;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints;

public abstract class LlmClientBase : BaseViewModel, ILLMChatClient
{
    public abstract string Name { get; }

    public abstract ILLMAPIEndpoint Endpoint { get; }

    [JsonIgnore] public abstract IEndpointModel Model { get; }


    public bool IsResponding
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    protected abstract IChatClient GetChatClient();

    protected virtual void ApplyChatOptions(ChatOptions chatOptions)
    {
        var modelInfo = this.Model;
        var modelParams = this.Parameters;
        chatOptions.ModelId = modelInfo.APIId;
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

        if (this.Model.Reasonable && modelParams.ThinkingEnabled)
        {
            var thinkingConfig =
                IThinkingConfig.CreateFrom(this.Endpoint, modelParams.ThinkingConfig as ThinkingConfigViewModel);
            thinkingConfig?.ApplyThinking(chatOptions);
        }
    }


    private readonly Stopwatch _durationStopwatch = new();

    private readonly Stopwatch _latencyStopwatch = new();


    private const string ToolCalls = "ToolCalls";

    [Experimental("SKEXP0001")]
    public virtual async Task<ChatCallResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
/*#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif*/
        var chatMessages = await context.GetMessagesAsync(cancellationToken);
        var result = new ChatCallResult();
        try
        {
            IsResponding = true;
            var preparedContext = await context.PrepareAsync(this.Model, interactor, cancellationToken);
            var functionCallEngine = preparedContext.FunctionCallEngine;
            var kernelPluginCollection = functionCallEngine.KernelPluginCollection;
            var chatHistory = preparedContext.ChatHistory;
            var requestOptions = preparedContext.RequestOptions;
            ApplyChatOptions(requestOptions);
            if (!this.Model.SupportFunctionCall && requestOptions.Tools?.Count > 0)
            {
                throw new NotSupportedException(
                    "This model does not support function calls, but tools were provided.");
            }

            chatHistory.AddRange(chatMessages);
            switch (Model.ThinkingIncludeMode)
            {
                case ThinkingIncludeMode.None:
                    //remove all thinking content from chat history except the last user message
                    chatHistory = chatHistory.Select(message =>
                        message.Role == ChatRole.Assistant ? message.GetReasoningTrimmedMessage() : message).ToList();
                    break;
                case ThinkingIncludeMode.All:
                    //do nothing, keep all thinking content
                    break;
                case ThinkingIncludeMode.KeepLast:
                    //remove all thinking content except the last user message
                    var lastAssistantIndex =
                        chatHistory.FindLastIndex(message => message.Role == ChatRole.Assistant) - 1;
                    for (int i = lastAssistantIndex; i >= 0; i--)
                    {
                        var chatMessage = chatHistory[i];
                        if (chatMessage.Role != ChatRole.Assistant) continue;
                        chatHistory[i] = chatMessage.GetReasoningTrimmedMessage();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var tempAdditionalProperties = preparedContext.TempAdditionalProperties;
            var requestOptionsAdditionalProperties = requestOptions.AdditionalProperties;
            if (tempAdditionalProperties != null && requestOptionsAdditionalProperties != null)
            {
                //由于openai库的实现不使用requestoptions.AdditionalProperties传递额外参数，
                //所以这里需要把临时属性也添加进去
                foreach (var requestOptionsAdditionalProperty in requestOptionsAdditionalProperties)
                {
                    tempAdditionalProperties[requestOptionsAdditionalProperty.Key] =
                        requestOptionsAdditionalProperty.Value;
                }
            }

            Exception? exception = null;
            int? totalLatency = null;
            var chatClient = GetChatClient();
            //本次响应的消息列表
            var responseMessages = new List<ChatMessage>();
            var preUpdates = new List<ChatResponseUpdate>();
            var totalUsageDetails = new UsageDetails
            {
                InputTokenCount = 0,
                TotalTokenCount = 0,
                OutputTokenCount = 0,
            };
            ChatFinishReason? finishReason = null;
            var streaming = this.Model.SupportStreaming && this.Parameters.Streaming;
            var softFunctionCall = false;
            if (kernelPluginCollection.Count > 0)
            {
                softFunctionCall = context.CallEngineType == FunctionCallEngineType.Prompt;
                //在openai调用引擎下，如果不可流式输出，则关闭流式输出
                if (!this.Model.FunctionCallOnStreaming &&
                    functionCallEngine is DefaultFunctionCallEngine)
                {
                    streaming = false;
                }
            }

            var chatContext = new ChatContext(interactor, tempAdditionalProperties)
                { Streaming = streaming, ShowRequestJson = context.IsDebugMode };
            _durationStopwatch.Reset();
            using (AsyncContextStore<ChatContext>.CreateInstance(chatContext))
            {
                while (true)
                {
                    var reasoningStart = false;
                    var reasoningEnd = false;
                    cancellationToken.ThrowIfCancellationRequested();
                    UsageDetails? loopUsageDetails = null;
                    try
                    {
                        _latencyStopwatch.Restart();
                        if (!_durationStopwatch.IsRunning)
                        {
                            _durationStopwatch.Start();
                        }

                        ChatResponse? preResponse;
                        if (streaming)
                        {
                            int? loopLatency = null;
                            await foreach (var update in chatClient
                                               .GetStreamingResponseAsync(chatHistory, requestOptions,
                                                   cancellationToken))
                            {
                                loopLatency ??= (int)_latencyStopwatch.ElapsedMilliseconds;
                                update.TryAddExtendedData();
                                preUpdates.Add(update);
                                chatContext.CompleteStreamResponse(result, update);
                                //只收集文本内容
                                interactor?.Info(GetText(update, ref reasoningStart, ref reasoningEnd));
                            }

                            preResponse = preUpdates.MergeResponse();
                            if (totalLatency == null)
                                totalLatency = loopLatency;
                            else
                                totalLatency += loopLatency;
                            preUpdates.Clear();
                        }
                        else
                        {
                            preResponse =
                                await chatClient.GetResponseAsync(chatHistory, requestOptions, cancellationToken);
                            //只收集文本内容
                            interactor?.WriteLine(preResponse.Text);
                            await chatContext.CompleteResponse(preResponse, result);
                        }

                        _durationStopwatch.Stop();
                        var preResponseMessages = preResponse.Messages;
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
                                    case TextReasoningContent:
                                        //do nothing, textContent & reasoningContent is already added to RespondingText
                                        break;
                                    case FunctionCallContent functionCallContent:
                                        interactor?.WriteLine("Function call requested: " +
                                                              functionCallContent.GetDebuggerString());
                                        break;
                                    case FunctionResultContent functionResultContent:
                                        interactor?.WriteLine(functionResultContent.GetDebuggerString());
                                        break;
                                    case ErrorContent errorContent:
                                        interactor?.Error(
                                            string.Format("Error content received: {0}, {1}, {2}",
                                                errorContent.Message, errorContent.Details, errorContent.ErrorCode));
                                        break;
                                    default:
                                        interactor?.Info("Received content:" + content.GetType().Name);
                                        /*interactor?.Warning(
                                            $"Unsupported content type: {content.GetType().Name}");
                                        interactor?.Warning(content.RawRepresentation?.ToString() ?? string.Empty);*/
                                        break;
                                }
                            }
                        }

                        result.ValidCallTimes++;
                        chatHistory.AddRange(preResponseMessages);
                        responseMessages.AddRange(preResponseMessages);
                        loopUsageDetails ??= preResponse.GetUsageDetailsFromAdditional();
                        if (loopUsageDetails != null)
                        {
                            totalUsageDetails.Add(loopUsageDetails);
                        }

                        finishReason = preResponse.FinishReason ?? preResponse.GetFinishReasonFromAdditional();
                        var finishReasonValue = finishReason?.Value;
                        if (finishReasonValue == null)
                        {
                            interactor?.Warning("Finish reason is null");
                        }
                        else
                        {
                            if (finishReasonValue.Equals(ChatFinishReason.ToolCalls.Value) ||
                                finishReasonValue.Equals(ToolCalls, StringComparison.OrdinalIgnoreCase))
                            {
                                interactor?.Info("Function call detect, need run function calls...");
                            }
                            else if (finishReason == ChatFinishReason.Stop)
                            {
                                if (!softFunctionCall)
                                {
                                    interactor?.Info("Response completed without function calls.");
                                    break;
                                }
                            }
                            else if (finishReason == ChatFinishReason.Length)
                            {
                                interactor?.Info("Exceeded maximum response length.");
                                exception = new OutOfContextWindowException()
                                {
                                    ChatResponse = preResponse
                                };
                                break;
                            }
                            else if (finishReason == ChatFinishReason.ContentFilter)
                            {
                                interactor?.Warning("Response was filtered by content filter.");
                                break;
                            }
                            else
                            {
                                interactor?.Warning($"Unexpected finish reason: {finishReason}");
                            }
                        }

                        List<FunctionCallContent> preFunctionCalls;
                        try
                        {
                            preFunctionCalls = await functionCallEngine.TryParseFunctionCalls(preResponse);
                        }
                        catch (Exception e)
                        {
                            interactor?.Error("Failed to parse function calls: " + e.HierarchicalMessage());
                            exception = e;
                            break;
                        }

                        if (preFunctionCalls.Count == 0)
                        {
                            interactor?.Info("No function calls were requested, response completed.");
                            break;
                        }

                        if (kernelPluginCollection.Count == 0)
                        {
                            exception =
                                new Exception(
                                    $"No functions available to call. But {preFunctionCalls.Count} function calls were requested.");
                            break;
                        }

                        interactor?.WriteLine("Processing function calls...");
                        var chatMessage = new ChatMessage();
                        chatHistory.Add(chatMessage);
                        responseMessages.Add(chatMessage);
                        await functionCallEngine.ProcessFunctionCallsAsync(chatContext, chatMessage, preFunctionCalls,
                            interactor, cancellationToken);
                    }
                    catch (OperationCanceledException canceledException)
                    {
                        exception = canceledException;
                        if (preUpdates.Count != 0)
                        {
                            responseMessages.AddRange(preUpdates.ToChatResponse().Messages);
                        }

                        break;
                    }
                    catch (HttpOperationException httpOperationException)
                    {
                        exception = new Exception($"Response Content: {httpOperationException.ResponseContent}",
                            httpOperationException);
                        if (exception.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase))
                        {
                            exception = new OutOfContextWindowException(httpOperationException);
                        }
                    }
                    catch (ClientResultException clientResultException)
                    {
                        var errorMessage = string.Empty;
                        var pipelineResponse = clientResultException.GetRawResponse();
                        var s = pipelineResponse?.Content.ToString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            errorMessage += $"Response Content: {s}";
                        }

                        exception = clientResultException.Status == 400
                            ? new LlmInvalidRequestException(errorMessage)
                            : new Exception(errorMessage, clientResultException);
                        if (exception.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase))
                        {
                            exception = new OutOfContextWindowException(clientResultException);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (preUpdates.Count != 0)
                        {
                            responseMessages.AddRange(preUpdates.ToChatResponse().Messages);
                        }

                        exception = new CriticalException(
                            "An unhandled exception occurred during response processing.", ex);
                        interactor?.Error($"Error during response: {ex}");
                    }

                    if (exception != null)
                    {
                        break;
                    }

                    interactor?.WriteLine();
                }
            }

            var duration = (int)Math.Ceiling(_durationStopwatch.ElapsedMilliseconds / 1000f);
            var price = this.Model.PriceCalculator?.Calculate(totalUsageDetails);
            result.Usage = totalUsageDetails;
            result.Messages = responseMessages;
            result.Exception = exception;
            result.Latency = totalLatency ?? 0;
            result.Duration = duration;
            result.FinishReason = finishReason;
            result.Price = price;
            var modelTelemetry = this.Model.Telemetry;
            if (modelTelemetry == null)
            {
                this.Model.Telemetry = new UsageCounter(result);
            }
            else
            {
                modelTelemetry.Add(result);
            }

            return result;
        }
        finally
        {
            IsResponding = false;
        }

        string GetText(ChatResponseUpdate update, ref bool reasoningStart, ref bool reasoningEnd)
        {
            var stringBuilder = new StringBuilder();
            foreach (var content in update.Contents)
            {
                if (content is TextContent textContent)
                {
                    if (reasoningStart && !reasoningEnd)
                    {
                        stringBuilder.AppendLine(ThinkBlockParser.CloseTag);
                        reasoningEnd = true;
                    }

                    stringBuilder.Append(textContent.Text);
                }
                else if (content is TextReasoningContent reasoningContent)
                {
                    if (!reasoningStart)
                    {
                        stringBuilder.AppendLine(ThinkBlockParser.OpenTag);
                        reasoningStart = true;
                    }

                    stringBuilder.Append(reasoningContent.Text);
                }
            }

            return stringBuilder.ToString();
        }
    }
}