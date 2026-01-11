using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.Rag;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    [JsonIgnore] public abstract ILLMModel Model { get; }

    /// <summary>
    /// 默认情况下，应该让LLM了解函数调用失败的情况，并继续生成内容。
    /// </summary>
    public bool IsQuitWhenFunctionCallFailed { get; set; } = false;

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

    private Lazy<ITokensCounter> _tokensCounterLazy =
        new Lazy<ITokensCounter>(() => ServiceLocator.GetService<ITokensCounter>()!);

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    protected abstract IChatClient GetChatClient();

    protected virtual ChatOptions CreateChatOptions()
    {
        var modelInfo = this.Model;
        var modelParams = this.Parameters;
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

        if (this.Model.Reasonable && modelParams.ThinkingEnabled)
        {
            var thinkingConfig =
                IThinkingConfig.CreateFrom(this.Endpoint, modelParams.ThinkingConfig as ThinkingConfigViewModel);
            thinkingConfig?.ApplyThinking(chatOptions);
        }

        return chatOptions;
    }

    [Experimental("SKEXP0001")]
    private static async Task<bool> AddTools(IEnumerable<IAIFunctionGroup> functionGroups,
        StringBuilder toolsPromptBuilder,
        KernelPluginCollection kernelPluginCollection, CancellationToken cancellationToken)
    {
        var startCount = kernelPluginCollection.Count;
        foreach (var functionGroup in functionGroups)
        {
            await functionGroup.EnsureAsync(cancellationToken);
            if (!functionGroup.IsAvailable)
            {
                continue;
            }

            var availableTools = functionGroup.AvailableTools;
            if (availableTools == null || availableTools.Count == 0)
            {
                continue;
            }

            var functionGroupName = functionGroup.Name;
            var additionPrompt = functionGroup.AdditionPrompt;
            toolsPromptBuilder.AppendLine(string.IsNullOrEmpty(additionPrompt)
                ? functionGroupName
                : $"{functionGroupName}:{additionPrompt}");
            kernelPluginCollection.AddFromFunctions(functionGroupName,
                availableTools.Select(function => function.AsKernelFunction()));
        }

        if (kernelPluginCollection.Count != startCount) return true;
        return false;
    }

    private readonly Stopwatch _stopwatch = new Stopwatch();


    const string ToolCalls = "ToolCalls";

    [Experimental("SKEXP0001")]
    public virtual async Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif

        var result = new CompletedResult();
        try
        {
            IsResponding = true;
            var dialogItems = context.DialogItems;
            var systemPrompt = context.SystemPrompt;
            var requestViewItem = context.Request;
            var searchService = requestViewItem?.SearchOption;
            if (searchService != null)
            {
                await searchService.ApplySearch(context);
            }

            var chatHistory = new List<ChatMessage>();
            var kernelPluginCollection = new KernelPluginCollection();
            var additionalPromptBuilder = new StringBuilder();
            var functionGroups = requestViewItem?.FunctionGroups;
            if (functionGroups != null)
            {
                var toolsPromptBuilder = new StringBuilder();
                toolsPromptBuilder.AppendLine(
                    "For the following functions, you can call them by name with the required parameters:");
                if (await AddTools(functionGroups, toolsPromptBuilder, kernelPluginCollection, cancellationToken))
                {
                    additionalPromptBuilder.Append(toolsPromptBuilder);
                }
            }

            var ragSources = requestViewItem?.RagSources;
            if (ragSources != null && ragSources.Any(source => source.IsAvailable))
            {
                var resourceIndex = 0;
                foreach (var ragSource in ragSources)
                {
                    if (ragSource is RagFileBase ragFile)
                    {
                        ragFile.FileIndexInContext = resourceIndex;
                        resourceIndex++;
                    }
                }

                additionalPromptBuilder.AppendLine(
                    "For the following RAG(Retrieval-Augmented Generation) sources such as files, web contents, " +
                    "you can get information by call them with the required parameters:");
                await AddTools(ragSources, additionalPromptBuilder, kernelPluginCollection, cancellationToken);
            }

            if (kernelPluginCollection.Count > 0)
            {
                systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                    ? additionalPromptBuilder.ToString()
                    : $"{systemPrompt}\n{additionalPromptBuilder}";
            }

            if (this.Model.SupportSystemPrompt && !string.IsNullOrWhiteSpace(systemPrompt))
            {
                chatHistory.Add(new ChatMessage(ChatRole.System, systemPrompt));
            }

            var requestOptions = this.CreateChatOptions();
            requestOptions.ResponseFormat = requestViewItem?.ResponseFormat;
            FunctionCallEngine functionCallEngine;
            if (!this.Model.SupportFunctionCall)
            {
                //如果不原生支持函数调用，切换到prompt实现
                functionCallEngine = FunctionCallEngine.Create(FunctionCallEngineType.Prompt,
                    kernelPluginCollection);
            }
            else
            {
                functionCallEngine =
                    FunctionCallEngine.Create(requestViewItem?.CallEngine ?? FunctionCallEngineType.OpenAI,
                        kernelPluginCollection);
            }

            if (kernelPluginCollection.Count > 0)
            {
                functionCallEngine.Initialize(requestOptions, Model, chatHistory);
            }

            if (!this.Model.SupportFunctionCall && requestOptions.Tools?.Count > 0)
            {
                throw new NotSupportedException(
                    "This model does not support function calls, but tools were provided.");
            }

            foreach (var dialogItem in dialogItems)
            {
                await foreach (var message in dialogItem.GetMessagesAsync(cancellationToken))
                {
                    chatHistory.Add(message);
                }
            }

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

            var tempAdditionalProperties = requestViewItem?.TempAdditionalProperties;
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

            string? errorMessage = null;
            int? latency = null;
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
                softFunctionCall = requestViewItem?.CallEngine == FunctionCallEngineType.Prompt;
                //在openai调用引擎下，如果不可流式输出，则关闭流式输出
                if (!this.Model.FunctionCallOnStreaming &&
                    functionCallEngine.GetType() == typeof(DefaultFunctionCallEngine))
                {
                    streaming = false;
                }
            }

            var chatContext = new ChatContext(interactor, tempAdditionalProperties)
                { Streaming = streaming };
            using (AsyncContextStore<ChatContext>.CreateInstance(chatContext))
            {
                _stopwatch.Reset();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UsageDetails? loopUsageDetails = null;
                    try
                    {
                        if (!_stopwatch.IsRunning)
                        {
                            _stopwatch.Start();
                        }

                        ChatResponse? preResponse;
                        if (streaming)
                        {
                            await foreach (var update in chatClient
                                               .GetStreamingResponseAsync(chatHistory, requestOptions,
                                                   cancellationToken))
                            {
                                latency ??= (int)_stopwatch.ElapsedMilliseconds;
                                update.TryAddExtendedData();
                                preUpdates.Add(update);
                                chatContext.CompleteStreamResponse(result, update);
                                //只收集文本内容
                                interactor?.Info(update.GetText());
                            }

                            preResponse = preUpdates.MergeResponse();
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

                        _stopwatch.Stop();
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
                                throw new OutOfContextWindowException()
                                {
                                    ChatResponse = preResponse
                                };
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


                        if (!functionCallEngine.TryParseFunctionCalls(preResponse, out var preFunctionCalls))
                        {
                            interactor?.Info("No function calls were requested, response completed.");
                            break;
                        }

                        if (kernelPluginCollection.Count == 0)
                        {
                            errorMessage =
                                $"No functions available to call. But {preFunctionCalls.Count} function calls were requested.";
                            break;
                        }

                        #region function call

                        interactor?.WriteLine("Processing function calls...");
                        var chatMessage = new ChatMessage();
                        chatHistory.Add(chatMessage);
                        responseMessages.Add(chatMessage);
                        var functionResultContents = new List<FunctionResultContent>();
                        foreach (var functionCallContent in preFunctionCalls)
                        {
                            if (!kernelPluginCollection.TryGetFunction(null, functionCallContent.Name,
                                    out var kernelFunction))
                            {
                                interactor?.Error(
                                    $"Function '{functionCallContent.Name}' not exist, call failed.");
                                functionResultContents.Add(new FunctionResultContent(functionCallContent.CallId, null)
                                    { Exception = new Exception("Function not exist") });
                                if (IsQuitWhenFunctionCallFailed)
                                {
                                    errorMessage = $"Function '{functionCallContent.Name}' not exist.";
                                    break;
                                }
                            }
                            else
                            {
                                var additionalFunctionCallResults = chatContext.AdditionalFunctionCallResult;
                                // additionalFunctionCallResults不能包含 FunctionResultContent
                                Trace.Assert(additionalFunctionCallResults.All(c => c is not FunctionResultContent));
                                var additionalUserMessageBuilder = chatContext.AdditionalUserMessage;
                                var chatMessageContents = chatMessage.Contents;
                                try
                                {
                                    var arguments = new AIFunctionArguments(functionCallContent.Arguments);
                                    additionalFunctionCallResults.Clear();
                                    additionalUserMessageBuilder.Clear();
                                    var invokeResult = await kernelFunction.InvokeAsync(arguments, cancellationToken);
                                    interactor?.WriteLine(
                                        $"Function '{functionCallContent.Name}' invoked successfully, result: {invokeResult}");
                                    functionResultContents.Add(new FunctionResultContent(functionCallContent.CallId,
                                        invokeResult));

                                    //用于特殊需求，某些函数调用后，可能需要额外的内容返回给LLM
                                    var additionalUserMessage = additionalUserMessageBuilder.ToString();
                                    if (!string.IsNullOrEmpty(additionalUserMessage))
                                    {
                                        chatMessageContents.Add(new TextContent(
                                            $"For function {functionCallContent.Name} call (call id:{functionCallContent.CallId}) result: {additionalUserMessage}"));
                                    }

                                    foreach (var additionalFunctionCallResult in additionalFunctionCallResults)
                                    {
                                        chatMessageContents.Add(additionalFunctionCallResult);
                                    }
                                }
                                catch (Exception e)
                                {
                                    interactor?.Error("Function call failed: " + e.HierarchicalMessage());
                                    functionResultContents.Add(
                                        new FunctionResultContent(functionCallContent.CallId, null)
                                            { Exception = e });
                                    if (IsQuitWhenFunctionCallFailed)
                                    {
                                        errorMessage =
                                            $"Function '{functionCallContent.Name}' invocation failed: {e.HierarchicalMessage()}";
                                        break;
                                    }
                                }
                            }
                        }

                        functionCallEngine.EncapsulateResult(chatMessage, functionResultContents);

                        #endregion
                    }
                    catch (OperationCanceledException)
                    {
                        errorMessage = "Operation was canceled.";
                        if (preUpdates.Count != 0)
                        {
                            responseMessages.AddRange(preUpdates.ToChatResponse().Messages);
                        }

                        break;
                    }
                    catch (LlmBadRequestException)
                    {
                        //对于错误请求，直接抛出，因为此时输入已经错误
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (preUpdates.Count != 0)
                        {
                            responseMessages.AddRange(preUpdates.ToChatResponse().Messages);
                        }

                        errorMessage = ex.HierarchicalMessage();
                        if (ex is HttpOperationException { ResponseContent: not null } httpOperationException)
                        {
                            errorMessage += $"\nResponse Content: {httpOperationException.ResponseContent}";
                        }
                        else if (ex is ClientResultException clientResultException)
                        {
                            var pipelineResponse = clientResultException.GetRawResponse();
                            var s = pipelineResponse?.Content.ToString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                errorMessage += $"\nResponse Content: {s}";
                            }

                            if (clientResultException.Status == 400)
                            {
                                throw new LlmBadRequestException(errorMessage);
                            }
                        }

                        interactor?.Error($"Error during response: {ex}");
                        if (errorMessage.Contains("context_length_exceeded"))
                        {
                            throw new OutOfContextWindowException();
                        }
                    }

                    if (errorMessage != null)
                    {
                        break;
                    }

                    interactor?.WriteLine();
                }
            }

            var duration = (int)Math.Ceiling(_stopwatch.ElapsedMilliseconds / 1000f);
            var price = this.Model.PriceCalculator?.Calculate(totalUsageDetails);
            result.Usage = totalUsageDetails;
            result.ResponseMessages = responseMessages;
            result.ErrorMessage = errorMessage;
            result.Latency = latency ?? 0;
            result.Duration = duration;
            result.FinishReason = finishReason;
            result.Price = price;
            if (!result.IsInterrupt)
            {
                var modelTelemetry = this.Model.Telemetry;
                if (modelTelemetry == null)
                {
                    this.Model.Telemetry = new UsageCount(result);
                }
                else
                {
                    modelTelemetry.Add(result);
                }
            }

            return result;
        }
        finally
        {
            IsResponding = false;
        }
    }
}