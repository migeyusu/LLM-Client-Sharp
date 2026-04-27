using System.ClientModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Elsa.Extensions;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Dialog.Proc;
using LLMClient.Endpoints.OpenAIAPI;
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

    private readonly ITokensCounter _tokensCounter;

    public IModelParams Parameters { get; set; } = new DefaultModelParam();

    protected abstract IChatClient GetChatClient(IRequestContext context);

    protected virtual void ApplyChatOptions(ChatOptions chatOptions)
    {
        /*MapperConfiguration mapperConfiguration = new MapperConfiguration((expression =>
        {
            expression.CreateMap<IEndpointModel,ChatOptions>()
                .ForMember((options => options.TopP),(configurationExpression => configurationExpression.Condition((model => model.TopPEnable)),));
        }));*/
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

    protected LlmClientBase(ITokensCounter tokensCounter)
    {
        _tokensCounter = tokensCounter;
    }

    private const string ToolCalls = "ToolCalls";

    private ChatHistoryCompressionStrategyFactory? HistoryCompressionFactory =>
        ServiceLocator.GetService<ChatHistoryCompressionStrategyFactory>();

    private static bool IsMalformedOpenAiCompatibleResponse(InvalidOperationException exception)
    {
        return exception.Message.Contains("requires an element of type 'Array'", StringComparison.OrdinalIgnoreCase)
               && exception.Message.Contains("type 'Null'", StringComparison.OrdinalIgnoreCase);
    }

    private static LlmInvalidRequestException CreateMalformedOpenAiCompatibleResponseException(
        ChatStackContext chatContext, InvalidOperationException innerException)
    {
        var message =
            "The LLM endpoint returned an invalid OpenAI-compatible response. Expected 'choices' to be an array.";
        var rawResponse = TryGetRawResponseText(chatContext.ResponseResult);
        if (!string.IsNullOrWhiteSpace(rawResponse))
        {
            message += $"{Environment.NewLine}Response Content: {TrimResponseForError(rawResponse)}";
        }

        return new LlmInvalidRequestException(message, innerException);
    }

    private static string? TryGetRawResponseText(ClientResult? result)
    {
        try
        {
            if (result == null)
            {
                return null;
            }

            return result.GetRawResponse().Content.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string TrimResponseForError(string responseText)
    {
        const int maxLength = 2048;
        if (responseText.Length <= maxLength)
        {
            return responseText;
        }

        return responseText[..maxLength] + "...";
    }

    [Experimental("SKEXP0001")]
    public virtual async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext requestContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            IsResponding = true;
            var functionCallEngine = requestContext.FunctionCallEngine;
            var requestOptions = requestContext.RequestOptions;
            ApplyChatOptions(requestOptions);
            if (!this.Model.SupportFunctionCall && requestOptions.Tools?.Count > 0)
            {
                throw new NotSupportedException(
                    "This model does not support function calls, but tools were provided.");
            }

            List<ChatMessage> chatMessages;
            var chatHistory = requestContext.ReadonlyHistory;
            switch (Model.ThinkingIncludeMode)
            {
                case ThinkingIncludeMode.None:
                    //remove all thinking content from chat history except the last user message
                    chatMessages = chatHistory.Select(message =>
                        message.Role == ChatRole.Assistant ? message.GetReasoningTrimmedMessage() : message).ToList();
                    break;
                case ThinkingIncludeMode.All:
                    //do nothing, keep all thinking content
                    chatMessages = chatHistory.ToList();
                    break;
                case ThinkingIncludeMode.KeepLast:
                    //remove all thinking content except the last user message
                    chatMessages = chatHistory.ToList();
                    var lastAssistantIndex =
                        chatMessages.FindLastIndex(message => message.Role == ChatRole.Assistant) - 1;
                    for (int i = lastAssistantIndex; i >= 0; i--)
                    {
                        var chatMessage = chatMessages[i];
                        if (chatMessage.Role != ChatRole.Assistant) continue;
                        chatMessages[i] = chatMessage.GetReasoningTrimmedMessage();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var tempAdditionalProperties = requestContext.TempAdditionalProperties;
            var requestOptionsAdditionalProperties = requestOptions.AdditionalProperties;
            if (tempAdditionalProperties != null && requestOptionsAdditionalProperties != null)
            {
                //由于openai库的实现不使用requestOptions.AdditionalProperties传递额外参数，
                //所以这里需要把临时属性也添加进去
                foreach (var requestOptionsAdditionalProperty in requestOptionsAdditionalProperties)
                {
                    tempAdditionalProperties[requestOptionsAdditionalProperty.Key] =
                        requestOptionsAdditionalProperty.Value;
                }
            }

            var chatClient = GetChatClient(requestContext);
            var streaming = Model.SupportStreaming && this.Parameters.Streaming;
            var softFunctionCall = false;
            if (functionCallEngine.HasFunctions)
            {
                softFunctionCall = !functionCallEngine.IsToolCallMode;
                //在openai调用引擎下，如果不可流式输出，则关闭流式输出
                if (!this.Model.FunctionCallOnStreaming &&
                    functionCallEngine.IsToolCallMode)
                {
                    streaming = false;
                }
            }

            var parentContext = AsyncContextStore<ChatStackContext>.Current;
            var chatContext = new ChatStackContext(tempAdditionalProperties)
            {
                WorkingDirectory = requestContext.WorkingDirectory ?? Directory.GetCurrentDirectory(),
                Streaming = streaming,
                ShowRequestJson = requestContext.ShowRequestJson,
                AutoApproveAllInvocations = requestContext.AutoApproveAllInvocations ||
                                            parentContext?.AutoApproveAllInvocations == true
            };
            var historyCompressionOptions = Model.HistoryCompression ?? ServiceLocator.GetService<GlobalOptions>()?.HistoryCompression ?? new ReactHistoryCompressionOptions();
            var historyCompressionStrategy = HistoryCompressionFactory?.Create(historyCompressionOptions.Mode);
            var dialogId = requestContext.DialogId;
            if (requestContext.ContextProviders != null)
            {
                chatClient = chatClient.UseContextProvider(requestContext.ContextProviders);
            }

            var segmentedReactHistory = requestContext.ReadonlyHistory.SegmentReactLevel(dialogId);
            // 基于历史中已有的该 agent 最大 round number 初始化，避免同一 Agent 多次调用时编号冲突
            var reactRoundNumber = segmentedReactHistory.MaxRoundNumber;
            using (AsyncContextStore<ChatStackContext>.CreateInstance(chatContext))
            {
                var step = new ReactStep();
                var compressionContext = new ChatHistoryContext
                {
                    Options = historyCompressionOptions,
                    CurrentRoundNumber = 0,
                    CurrentClient = this,
                    Step = step,
                    History = segmentedReactHistory
                };

                if (reactRoundNumber > 1 && historyCompressionStrategy != null)
                {
                    //上一次发生中断情况下
                    compressionContext.CurrentRoundNumber = reactRoundNumber;
                    await InTaskCompressIfNeedAsync(historyCompressionStrategy,
                        compressionContext, cancellationToken);
                }
/*#if DEBUG

                var estimateTokens = await _tokensCounter.CountTokens(chatMessages);
                Debug.Write($"request tokens:{estimateTokens}");
#endif*/

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    chatContext.CurrentStep = step;
                    compressionContext.Step = step;
                    // 启动后台生产任务
                    var producerTask = ProduceStepAsync(
                        step, chatContext, chatClient, requestOptions,
                        functionCallEngine, streaming, softFunctionCall, ++reactRoundNumber, historyCompressionStrategy,
                        compressionContext,
                        cancellationToken);

                    // 立即 yield —— 消费者可以马上开始 await foreach (var evt in step)
                    yield return step;

                    // 等待该轮生产完成
                    await producerTask;
                    var result = step.Result!;
                    // 更新 telemetry
                    if (result.Usage != null)
                    {
                        var price = this.Model.PriceCalculator?.Calculate(result.Usage);
                        result.Price = price;
                    }

                    var modelTelemetry = this.Model.Telemetry;
                    if (modelTelemetry == null)
                    {
                        this.Model.Telemetry = new UsageCounter(result);
                    }
                    else
                    {
                        modelTelemetry.Add(result);
                    }

                    if (result.IsCompleted || result.Exception != null)
                        break;
                    step = new ReactStep();
                }
            }
        }
        finally
        {
            IsResponding = false;
        }
    }

    [Experimental("SKEXP0001")]
    private async Task ProduceStepAsync(
        ReactStep step,
        ChatStackContext stackContext,
        IChatClient chatClient,
        ChatOptions requestOptions,
        FunctionCallEngine functionCallEngine,
        bool streaming,
        bool softFunctionCall,
        int reactRoundNumber,
        IChatHistoryCompressionStrategy? strategy,
        ChatHistoryContext context,
        CancellationToken cancellationToken)
    {
        var reasoningStart = false;
        var reasoningEnd = false;
        ChatMessage? functionResultMessage = null;
        UsageDetails? loopUsageDetails = null;
        Exception? exception = null;
        ChatFinishReason? finishReason = null;
        var currentRound = new ReactRound() { RoundNumber = reactRoundNumber };
        var preUpdates = new List<ChatResponseUpdate>();
        var stepResult = new StepResult() { MaxContextTokens = Model.MaxContextSize };
        int? latency = null;
        try
        {
            _durationStopwatch.Restart();
            ChatResponse? preResponse;
            IReadOnlyList<ChatMessage> requestMessages = context.RequestMessages.ToArray();
            if (streaming)
            {
                _latencyStopwatch.Restart();
                await foreach (var update in chatClient
                                   .GetStreamingResponseAsync(requestMessages, requestOptions,
                                       cancellationToken))
                {
                    latency ??= (int)_latencyStopwatch.ElapsedMilliseconds;
                    update.TryAddExtendedData();
                    preUpdates.Add(update);
                    stackContext.CompleteStreamResponse(stepResult, update);
                    // 发射流式事件
                    EmitStreamEvents(step, update, ref reasoningStart, ref reasoningEnd);
                }

                preResponse = preUpdates.MergeResponse();
                preUpdates.Clear();
            }
            else
            {
                preResponse =
                    await chatClient.GetResponseAsync(requestMessages, requestOptions, cancellationToken);
                // 发射文本内容
                if (!string.IsNullOrEmpty(preResponse.Text))
                {
                    step.EmitText(preResponse.Text);
                }

                await stackContext.CompleteResponse(preResponse, stepResult);
            }

            _durationStopwatch.Stop();
            loopUsageDetails = preResponse.Usage;
            var preResponseMessage = preResponse.Messages.ToSingle();
            if (reactRoundNumber > 1 && functionCallEngine.HasFunctions)
            {
                DeduplicateRepeatedThinking(preResponseMessage, requestMessages);
            }

            preResponseMessage.TagLoopLevel(reactRoundNumber, ReactHistoryMessageKind.Assistant);
            foreach (var content in preResponseMessage.Contents)
            {
                switch (content)
                {
                    case UsageContent usageContent:
                        (loopUsageDetails ??= new UsageDetails()).Add(usageContent.Details);
                        break;
                    case FunctionResultContent:
                    case TextContent:
                    case TextReasoningContent:
                        //do nothing, textContent & reasoningContent is already added to RespondingText
                        break;
                    case FunctionCallContent functionCallContent:
                        step.Emit(new FunctionCallStarted(functionCallContent));
                        break;
                    case ErrorContent errorContent:
                        step.EmitDiagnostic(DiagLevel.Error,
                            string.Format("Error content received: {0}, {1}, {2}",
                                errorContent.Message, errorContent.Details, errorContent.ErrorCode));
                        break;
                    default:
                        step.EmitDiagnostic(DiagLevel.Warning,
                            "Received content:" + content.GetType().Name);
                        break;
                }
            }

            currentRound.AssistantMessage = preResponseMessage;
            context.History.Rounds.Add(currentRound);
            loopUsageDetails ??= preResponse.GetUsageDetailsFromAdditional();
            if (loopUsageDetails != null)
            {
                if (loopUsageDetails.AdditionalCounts?.TryGetValue("PromptTokensDetails.CachedTokens",
                        out var cachedTokens) == true)
                {
                    loopUsageDetails.InputTokenCount += cachedTokens;
                }
            }

            finishReason = preResponse.FinishReason ?? preResponse.GetFinishReasonFromAdditional();
            var finishReasonValue = finishReason?.Value;
            if (finishReasonValue == null)
            {
                step.EmitDiagnostic(DiagLevel.Warning, "Finish reason is null");
            }
            else
            {
                if (finishReasonValue.Equals(ChatFinishReason.ToolCalls.Value) ||
                    finishReasonValue.Equals(ToolCalls, StringComparison.OrdinalIgnoreCase))
                {
                    step.EmitDiagnostic(DiagLevel.Info, "Function call detect, need run function calls...");
                }
                else if (finishReason == ChatFinishReason.Stop)
                {
                    if (!softFunctionCall)
                    {
                        stepResult.IsCompleted = true;
                        return;
                    }
                }
                else if (finishReason == ChatFinishReason.Length)
                {
                    exception = new OutOfContextWindowException(preResponse);
                    stepResult.IsCompleted = false;
                    return;
                }
                else if (finishReason == ChatFinishReason.ContentFilter)
                {
                    stepResult.IsCompleted = false;
                    exception = new ResultFilteredException();
                    return;
                }
                else
                {
                    step.EmitDiagnostic(DiagLevel.Warning, $"Unexpected finish reason: {finishReason}");
                }
            }

            List<FunctionCallContent> preFunctionCalls;
            try
            {
                preFunctionCalls = await functionCallEngine.TryParseFunctionCalls(preResponse);
            }
            catch (Exception e)
            {
                stepResult.IsCompleted = false;
                exception = e;
                return;
            }

            if (preFunctionCalls.Count == 0)
            {
                stepResult.IsCompleted = true;
                return;
            }

            if (!functionCallEngine.HasFunctions)
            {
                exception = new Exception(
                    $"No functions available to call. But {preFunctionCalls.Count} function calls were requested.");
                stepResult.IsCompleted = false;
                return;
            }

            step.EmitDiagnostic(DiagLevel.Info, "Processing function calls...");
            functionResultMessage = new ChatMessage();
            functionResultMessage.TagLoopLevel(reactRoundNumber,
                ReactHistoryMessageKind.Observation);
            currentRound.ObservationMessage = functionResultMessage;
            await functionCallEngine.ProcessFunctionCallsAsync(stackContext, functionResultMessage,
                preFunctionCalls, step, cancellationToken);
            if (strategy != null)
            {
                context.CurrentTokens = loopUsageDetails?.InputTokenCount + loopUsageDetails?.OutputTokenCount;
                context.CurrentRoundNumber = reactRoundNumber;
                await InTaskCompressIfNeedAsync(strategy, context, cancellationToken);
            }

            // 该轮未完成（有 function call），继续下一轮
            stepResult.IsCompleted = false;
        }
        catch (AgentFlowException agentFlowException)
        {
            //儘管已經結束，也要收納有效消息
            if (agentFlowException.Messages.Count > 0)
            {
                var chatMessage = agentFlowException.Messages.ToSingle();
                if (functionResultMessage != null)
                {
                    functionResultMessage.Contents.AddRange(chatMessage.Contents);
                }
                else
                {
                    chatMessage.TagLoopLevel(reactRoundNumber,
                        ReactHistoryMessageKind.Observation);
                    currentRound.ObservationMessage = chatMessage;
                }
            }

            stepResult.IsCompleted = true;
            exception = agentFlowException; //专门表示已经完成
        }
        catch (OperationCanceledException operationCanceledException)
        {
            AddLeaveHistory();
            exception = operationCanceledException;
            stepResult.IsCompleted = true;
        }
        catch (HttpOperationException httpOperationException)
        {
            AddLeaveHistory();
            exception = new Exception($"Response Content: {httpOperationException.ResponseContent}",
                httpOperationException);
            if (exception.Message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase))
            {
                exception = new OutOfContextWindowException(httpOperationException);
            }

            stepResult.IsCompleted = false;
        }
        catch (ClientResultException clientResultException)
        {
            AddLeaveHistory();
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

            stepResult.IsCompleted = false;
        }
        catch (LlmInvalidRequestException llmInvalidRequestException)
        {
            AddLeaveHistory();
            stepResult.IsCompleted = false;
            exception = llmInvalidRequestException;
        }
        catch (InvalidOperationException invalidOperationException)
            when (IsMalformedOpenAiCompatibleResponse(invalidOperationException))
        {
            AddLeaveHistory();
            exception = CreateMalformedOpenAiCompatibleResponseException(stackContext, invalidOperationException);
            stepResult.IsCompleted = false;
        }
        catch (Exception ex)
        {
            AddLeaveHistory();
            exception = new ChatCriticalException(
                "An unhandled exception occurred during response processing.", ex);
            stepResult.IsCompleted = false;
        }
        finally
        {
            stepResult.Exception = exception;
            stepResult.Usage = loopUsageDetails;
            stepResult.FinishReason = finishReason;
            stepResult.Latency = latency ?? 0;
            stepResult.Duration = (int)_durationStopwatch.Elapsed.TotalSeconds;
            stepResult.Messages = currentRound.Messages;
            stepResult.ProtocolLog = step.ProtocolLog;
            step.Complete(stepResult);
        }

        return;

        void AddLeaveHistory()
        {
            if (preUpdates.Count != 0)
            {
                currentRound.AssistantMessage = preUpdates.ToChatResponse().Messages.ToSingle();
            }
        }
    }

    private static void EmitStreamEvents(ReactStep step, ChatResponseUpdate update,
        ref bool reasoningStart, ref bool reasoningEnd)
    {
        foreach (var content in update.Contents)
        {
            if (content is TextContent textContent)
            {
                if (reasoningStart && !reasoningEnd)
                {
                    reasoningEnd = true;
                }

                step.EmitText(textContent.Text ?? "");
            }
            else if (content is TextReasoningContent reasoningContent)
            {
                if (!reasoningStart)
                {
                    reasoningStart = true;
                }

                step.EmitReasoning(reasoningContent.Text ?? "");
            }
        }
    }

    private async Task InTaskCompressIfNeedAsync(
        IChatHistoryCompressionStrategy historyCompressionStrategy, ChatHistoryContext context,
        CancellationToken cancellationToken)
    {
        if (context.CurrentTokens == null)
        {
            context.CurrentTokens = await _tokensCounter.CountTokens(context.History.AllMessages);
        }

        var historyCompressionOptions = context.Options;
        // 1. Token-based in-task compression trigger
        var compressionKind = GetHistoryCompressionKind(historyCompressionOptions.Mode);
        if (compressionKind.HasValue &&
            await ShouldRunInTaskCompression(context.CurrentTokens.Value, historyCompressionOptions))
        {
            // 2. Pre-process: uniformly replace error rounds outside the preserve window with summaries
            if (historyCompressionOptions.SummaryErrorLoop)
            {
                var errorSummaryChatHistoryCompressionStrategy =
                    ServiceLocator.GetService<ErrorSummaryChatHistoryCompressionStrategy>();
                errorSummaryChatHistoryCompressionStrategy?.CompressAsync(context, cancellationToken);
            }

            context.Step?.EmitHistoryCompressionStarted(compressionKind.Value);
            await historyCompressionStrategy.CompressAsync(context, cancellationToken);
            context.Step?.EmitHistoryCompressionCompleted(compressionKind.Value, context.CompressionApplied);
        }
    }

    private Task<bool> ShouldRunInTaskCompression(long totalTokensCount,
        ReactHistoryCompressionOptions options)
    {
        var threshold = options.ReactTokenThresholdPercent;
        if (threshold <= 0) return Task.FromResult(false);
        var maxContext = Model.MaxContextSize;
        if (maxContext <= 0) return Task.FromResult(false);

        return Task.FromResult(totalTokensCount > threshold * maxContext);
    }

    private static HistoryCompressionKind? GetHistoryCompressionKind(ReactHistoryCompressionMode mode)
    {
        return mode switch
        {
            ReactHistoryCompressionMode.ObservationMasking => HistoryCompressionKind.ObservationMasking,
            ReactHistoryCompressionMode.LoopSummary => HistoryCompressionKind.InfoCleaning,
            ReactHistoryCompressionMode.TaskSummary => HistoryCompressionKind.TaskSummary,
            _ => null,
        };
    }

    private static void DeduplicateRepeatedThinking(ChatMessage currentMessages,
        IReadOnlyList<ChatMessage> historyMessages)
    {
        var previousReasoningSet = GetLastAssistantReasoningSet(historyMessages);
        if (previousReasoningSet.Count == 0)
        {
            return;
        }

        if (currentMessages.Role != ChatRole.Assistant)
        {
            return;
        }

        for (int i = currentMessages.Contents.Count - 1; i >= 0; i--)
        {
            if (currentMessages.Contents[i] is not TextReasoningContent reasoningContent)
            {
                continue;
            }

            var normalized = NormalizeReasoning(reasoningContent.Text);
            if (normalized != null && previousReasoningSet.Contains(normalized))
            {
                currentMessages.Contents.RemoveAt(i);
            }
        }
    }

    private static HashSet<string> GetLastAssistantReasoningSet(IReadOnlyList<ChatMessage> historyMessages)
    {
        for (int i = historyMessages.Count - 1; i >= 0; i--)
        {
            var message = historyMessages[i];
            if (message.Role != ChatRole.Assistant)
            {
                continue;
            }

            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var content in message.Contents)
            {
                if (content is not TextReasoningContent reasoningContent)
                {
                    continue;
                }

                var normalized = NormalizeReasoning(reasoningContent.Text);
                if (normalized != null)
                {
                    result.Add(normalized);
                }
            }

            if (result.Count > 0)
            {
                return result;
            }
        }

        return [];
    }

    private static string? NormalizeReasoning(string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return null;
        }

        return reasoning
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}