using System.ClientModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Agent.MiniSWE;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Component.ViewModel;
using LLMClient.Dialog;
using LLMClient.Dialog.Proc;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Agents.AI;
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
    /// <summary>
    /// Re-entrancy guard: prevents recursive preamble compression when the summarizer
    /// calls SendRequestAsync on the same (or any) LlmClientBase instance.
    /// </summary>
    private static readonly AsyncLocal<bool> PreambleCompressionActive = new();

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

    protected abstract IChatClient GetChatClient();

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
        ChatContext chatContext, InvalidOperationException innerException)
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
                //由于openai库的实现不使用requestoptions.AdditionalProperties传递额外参数，
                //所以这里需要把临时属性也添加进去
                foreach (var requestOptionsAdditionalProperty in requestOptionsAdditionalProperties)
                {
                    tempAdditionalProperties[requestOptionsAdditionalProperty.Key] =
                        requestOptionsAdditionalProperty.Value;
                }
            }

            var chatClient = GetChatClient();
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

            var parentContext = AsyncContextStore<ChatContext>.Current;
            var chatContext = ChatContext.CreateForRequest(requestContext, tempAdditionalProperties,
                streaming, parentContext);
            var historyCompressionOptions = Model.HistoryCompression;
            var historyCompressionStrategy = HistoryCompressionFactory?.Create(historyCompressionOptions);
            var agentId = requestContext.AgentId;
            // 基于历史中已有的该 agent 最大 round number 初始化，避免同一 Agent 多次调用时编号冲突
            var reactRoundNumber = ReactHistorySegmenter.GetMaxRoundNumber(chatMessages, agentId);
            using (AsyncContextStore<ChatContext>.CreateInstance(chatContext))
            {
                var step = new ReactStep();
                var compressionContext = new ChatHistoryCompressionContext
                {
                    ChatHistory = chatMessages,
                    Options = historyCompressionOptions,
                    CurrentRound = 0,
                    CurrentClient = this,
                    Step = step,
                    AgentId = agentId,
                };

                await CompressPreambleIfNeededAsync(compressionContext, cancellationToken);
                if (reactRoundNumber > 1 && historyCompressionStrategy != null)
                {
                    //上一次发生中断情况下
                    compressionContext.CurrentRound = reactRoundNumber;
                    await InTaskCompressIfNeedAsync(historyCompressionStrategy,
                        compressionContext, cancellationToken);
                }
#if DEBUG

                var estimateTokens = await _tokensCounter.CountTokens(chatMessages);
                Debug.Write($"request tokens:{estimateTokens}");

#endif
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    chatContext.CurrentStep = step;

                    // 启动后台生产任务
                    var producerTask = ProduceStepAsync(
                        step, chatContext, chatClient, chatMessages, requestOptions,
                        functionCallEngine, streaming, softFunctionCall, ++reactRoundNumber, historyCompressionStrategy,
                        compressionContext, agentId,
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
        ChatContext chatContext,
        IChatClient chatClient,
        List<ChatMessage> chatMessages,
        ChatOptions requestOptions,
        FunctionCallEngine functionCallEngine,
        bool streaming,
        bool softFunctionCall,
        int reactRoundNumber,
        IChatHistoryCompressionStrategy? strategy,
        ChatHistoryCompressionContext context,
        string? agentId,
        CancellationToken cancellationToken)
    {
        var reasoningStart = false;
        var reasoningEnd = false;
        ChatMessage? functionResultMessage = null;
        UsageDetails? loopUsageDetails = null;
        Exception? exception = null;
        ChatFinishReason? finishReason = null;
        var loopMessages = new List<ChatMessage>();
        var preUpdates = new List<ChatResponseUpdate>();
        var stepResult = new StepResult() { MaxContextTokens = Model.MaxContextSize };
        int? latency = null;
        try
        {
            _durationStopwatch.Restart();
            ChatResponse? preResponse;
            if (streaming)
            {
                _latencyStopwatch.Restart();
                await foreach (var update in chatClient
                                   .GetStreamingResponseAsync(chatMessages, requestOptions,
                                       cancellationToken))
                {
                    latency ??= (int)_latencyStopwatch.ElapsedMilliseconds;
                    update.TryAddExtendedData();
                    preUpdates.Add(update);
                    chatContext.CompleteStreamResponse(stepResult, update);
                    // 发射流式事件
                    EmitStreamEvents(step, update, ref reasoningStart, ref reasoningEnd);
                }

                preResponse = preUpdates.MergeResponse();
                preUpdates.Clear();
            }
            else
            {
                preResponse =
                    await chatClient.GetResponseAsync(chatMessages, requestOptions, cancellationToken);
                // 发射文本内容
                if (!string.IsNullOrEmpty(preResponse.Text))
                {
                    step.EmitText(preResponse.Text);
                }

                await chatContext.CompleteResponse(preResponse, stepResult);
            }

            _durationStopwatch.Stop();
            loopUsageDetails = preResponse.Usage;
            var preResponseMessages = preResponse.Messages;
            if (reactRoundNumber > 1 && functionCallEngine.HasFunctions)
            {
                DeduplicateRepeatedThinking(preResponseMessages, chatMessages);
            }

            ReactHistorySegmenter.TagMessages(preResponseMessages, reactRoundNumber, ReactHistoryMessageKind.Assistant,
                agentId);
            foreach (var preResponseMessage in preResponseMessages)
            {
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
            }

            loopMessages.AddRange(preResponseMessages);
            loopUsageDetails ??= preResponse.GetUsageDetailsFromAdditional();
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
            ReactHistorySegmenter.TagMessage(functionResultMessage, reactRoundNumber,
                ReactHistoryMessageKind.Observation, agentId);
            loopMessages.Add(functionResultMessage);
            chatMessages.AddRange(loopMessages);
            await functionCallEngine.ProcessFunctionCallsAsync(chatContext, functionResultMessage,
                preFunctionCalls, step, cancellationToken);
            if (strategy != null)
            {
                context.CurrentTokens = loopUsageDetails?.TotalTokenCount;
                context.ChatHistory = chatMessages;
                context.Step = step;
                context.CurrentRound = reactRoundNumber;
                await InTaskCompressIfNeedAsync(strategy, context, cancellationToken);
            }

            // 该轮未完成（有 function call），继续下一轮
            stepResult.IsCompleted = false;
        }
        catch (AgentFlowException agentFlowException)
        {
            if (functionResultMessage is { Contents.Count: 0 })
            {
                loopMessages.Remove(functionResultMessage);
            }

            if (agentFlowException.Messages.Count > 0)
            {
                loopMessages.AddRange(agentFlowException.Messages);
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
            exception = CreateMalformedOpenAiCompatibleResponseException(chatContext, invalidOperationException);
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
            stepResult.Messages = loopMessages;
            stepResult.ProtocolLog = step.ProtocolLog;
            step.Complete(stepResult);
        }

        return;

        void AddLeaveHistory()
        {
            if (preUpdates.Count != 0)
            {
                loopMessages.AddRange(preUpdates.ToChatResponse().Messages);
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
        IChatHistoryCompressionStrategy historyCompressionStrategy, ChatHistoryCompressionContext context,
        CancellationToken cancellationToken)
    {
        if (context.CurrentTokens == null)
        {
            context.CurrentTokens = await _tokensCounter.CountTokens(context.ChatHistory);
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
                await PreprocessErrorRoundsAsync(context.ChatHistory, historyCompressionOptions, context.AgentId,
                    cancellationToken);
            }

            context.Step?.EmitHistoryCompressionStarted(compressionKind.Value);
            await historyCompressionStrategy.CompressAsync(context, cancellationToken);
            context.Step?.EmitHistoryCompressionCompleted(compressionKind.Value, context.CompressionApplied);
        }
    }

    private static async Task CompressPreambleIfNeededAsync(
        ChatHistoryCompressionContext compressionContext,
        CancellationToken cancellationToken)
    {
        if (!compressionContext.Options.PreambleCompression || PreambleCompressionActive.Value)
        {
            return;
        }

        var viewModelFactory = ServiceLocator.GetService<IViewModelFactory>();
        if (viewModelFactory == null)
        {
            return;
        }

        var preambleStrategy = viewModelFactory.Create<PreambleSummaryChatHistoryCompressionStrategy>();
        if (!await preambleStrategy.ShouldCompress(compressionContext))
        {
            return;
        }

        compressionContext.Step?.EmitHistoryCompressionStarted(HistoryCompressionKind.PreambleSummary);
        PreambleCompressionActive.Value = true;
        try
        {
            await preambleStrategy.CompressAsync(compressionContext, cancellationToken);
            compressionContext.Step?.EmitHistoryCompressionCompleted(HistoryCompressionKind.PreambleSummary,
                compressionContext.CompressionApplied);
        }
        finally
        {
            PreambleCompressionActive.Value = false;
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

    private async Task PreprocessErrorRoundsAsync(
        List<ChatMessage> chatMessages,
        ReactHistoryCompressionOptions options,
        string? agentId,
        CancellationToken cancellationToken)
    {
        var segmentation = ReactHistorySegmenter.Segment(chatMessages, agentId);
        var roundsToKeep = Math.Max(0, options.PreserveRecentRounds);
        var keepFromIndex = Math.Max(0, segmentation.Rounds.Count - roundsToKeep);

        var hasErrorsToProcess = segmentation.Rounds.Take(keepFromIndex).Any(round => round.HasError);
        if (!hasErrorsToProcess)
        {
            return;
        }

        var summarizer = ServiceLocator.GetService<Summarizer>();
        var replacement = new List<ChatMessage>(segmentation.PreambleMessages);
        for (var i = 0; i < segmentation.Rounds.Count; i++)
        {
            var round = segmentation.Rounds[i];
            if (i < keepFromIndex && round.HasError)
            {
                replacement.Add(await ReactErrorRoundSummarizer.BuildErrorSummaryMessageAsync(
                    round, summarizer, this, agentId, cancellationToken));
                continue;
            }

            replacement.AddRange(round.AssistantMessages);
            replacement.AddRange(round.ObservationMessages);
        }

        chatMessages.Clear();
        chatMessages.AddRange(replacement);
    }

    private static HistoryCompressionKind? GetHistoryCompressionKind(ReactHistoryCompressionMode mode)
    {
        return mode switch
        {
            ReactHistoryCompressionMode.ObservationMasking => HistoryCompressionKind.ObservationMasking,
            ReactHistoryCompressionMode.InfoCleaning => HistoryCompressionKind.InfoCleaning,
            ReactHistoryCompressionMode.TaskSummary => HistoryCompressionKind.TaskSummary,
            _ => null,
        };
    }

    private static void DeduplicateRepeatedThinking(IList<ChatMessage> currentMessages,
        IList<ChatMessage> historyMessages)
    {
        var previousReasoningSet = GetLastAssistantReasoningSet(historyMessages);
        if (previousReasoningSet.Count == 0)
        {
            return;
        }

        foreach (var message in currentMessages)
        {
            if (message.Role != ChatRole.Assistant)
            {
                continue;
            }

            for (int i = message.Contents.Count - 1; i >= 0; i--)
            {
                if (message.Contents[i] is not TextReasoningContent reasoningContent)
                {
                    continue;
                }

                var normalized = NormalizeReasoning(reasoningContent.Text);
                if (normalized != null && previousReasoningSet.Contains(normalized))
                {
                    message.Contents.RemoveAt(i);
                }
            }
        }
    }

    private static HashSet<string> GetLastAssistantReasoningSet(IList<ChatMessage> historyMessages)
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