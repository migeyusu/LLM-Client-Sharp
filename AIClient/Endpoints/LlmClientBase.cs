using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.MCP;
using LLMClient.Rag;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenAI.Chat;
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

    public abstract ILLMEndpoint Endpoint { get; }

    [JsonIgnore] public abstract ILLMChatModel Model { get; }

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

        return chatOptions;
    }

    [Experimental("SKEXP0001")]
    private async Task<bool> AddTools(IEnumerable<IAIFunctionGroup> functionGroups, StringBuilder toolsPromptBuilder,
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

            toolsPromptBuilder.AppendLine(functionGroup.AdditionPrompt);
            kernelPluginCollection.AddFromFunctions(functionGroup.Name,
                availableTools.Select(function => function.AsKernelFunction()));
        }

        if (kernelPluginCollection.Count != startCount) return true;
        return false;
    }

    private readonly Stopwatch _stopwatch = new Stopwatch();

    [Experimental("SKEXP0001")]
    public virtual async Task<CompletedResult> SendRequest(DialogContext context,
        Action<string>? stream = null, ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
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

            var thinkingConfig = requestViewItem?.ThinkingConfig;
            thinkingConfig?.EnableThinking(requestViewItem!);
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
            var functionCallEngine =
                FunctionCallEngine.Create(requestViewItem?.CallEngine ?? FunctionCallEngineType.OpenAI,
                    kernelPluginCollection);
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

            requestOptions.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            var additionalProperties = requestViewItem?.AdditionalProperties;
            if (additionalProperties != null)
            {
                foreach (var additionalProperty in additionalProperties)
                {
                    requestOptions.AdditionalProperties[additionalProperty.Key] = additionalProperty.Value;
                }
            }

            string? errorMessage = null;
            int? latency = null;
            var chatClient = GetChatClient();
            _stopwatch.Restart();
            //本次响应的消息列表
            var responseMessages = new List<ChatMessage>();
            var preUpdates = new List<ChatResponseUpdate>();
            var totalUsageDetails = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
            };
            ChatFinishReason? finishReason = null;
            var streaming = this.Model.SupportStreaming && this.Parameters.Streaming;
            if (kernelPluginCollection.Count > 0)
            {
                if (!this.Model.FunctionCallOnStreaming)
                {
                    streaming = false;
                }
            }

            var chatContext = new ChatContext(requestViewItem?.AdditionalProperties)
                { Streaming = streaming };
            using (AsyncContext<ChatContext>.Create(chatContext))
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UsageDetails? loopUsageDetails = null;
                    try
                    {
                        ChatResponse? preResponse;
                        if (streaming)
                        {
                            await foreach (var update in chatClient
                                               .GetStreamingResponseAsync(chatHistory, requestOptions,
                                                   cancellationToken))
                            {
                                latency ??= (int)_stopwatch.ElapsedMilliseconds;
                                preUpdates.Add(update);
                                chatContext.CompleteStreamResponse(result, update);
                                //只收集文本内容
                                AddStream(update.Text);
                            }

                            /*foreach (var chatResponseUpdate in preUpdates)
                            {
                                var textReasoningContents = chatResponseUpdate.Contents.OfType<TextReasoningContent>()
                                    .ToArray();
                                if (textReasoningContents.Any())
                                {

                                }

                            }*/
                            preResponse = preUpdates.ToChatResponse();
                            foreach (var message in preResponse.Messages)
                            {
                                var groupedContents = message.Contents
                                    .GroupBy(content => content.GetType())
                                    .ToArray();
                                message.Contents.Clear();
                                foreach (var group in groupedContents)
                                {
                                    var contentType = group.Key;
                                    if (contentType == typeof(TextContent))
                                    {
                                        var mergedText = string.Join("", group.Cast<TextContent>().Select(c => c.Text));
                                        message.Contents.Add(new TextContent(mergedText));
                                    }
                                    else if (contentType == typeof(TextReasoningContent))
                                    {
                                        var mergedText = string.Join("",
                                            group.Cast<TextReasoningContent>().Select(c => c.Text));
                                        message.Contents.Add(new TextReasoningContent(mergedText));
                                    }
                                    else if (contentType == typeof(UsageContent))
                                    {
                                        var mergedUsage = new UsageDetails();
                                        foreach (var content in group.Cast<UsageContent>())
                                        {
                                            mergedUsage.Add(content.Details);
                                        }

                                        message.Contents.Add(new UsageContent(mergedUsage));
                                    }
                                    else
                                    {
                                        foreach (var aiContent in group)
                                        {
                                            message.Contents.Add(aiContent);   
                                        }
                                    }
                                }
                            }

                            preUpdates.Clear();
                        }
                        else
                        {
                            preResponse =
                                await chatClient.GetResponseAsync(chatHistory, requestOptions, cancellationToken);
                            //只收集文本内容
                            AddStream(preResponse.Text);
                            await chatContext.CompleteResponse(preResponse, result);
                        }

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
                                        //do nothing, textContent is already added to RespondingText
                                        break;
                                    case FunctionCallContent functionCallContent:
                                        NewLine(functionCallContent.GetDebuggerString());
                                        break;
                                    case FunctionResultContent functionResultContent:
                                        NewLine(functionResultContent.GetDebuggerString());
                                        break;
                                    case TextReasoningContent reasoningContent:
                                        var text = reasoningContent.Text;
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            stream?.Invoke(Environment.NewLine);
                                            stream?.Invoke(text);
                                        }

                                        break;
                                    default:
                                        logger?.LogWarning("unsupported content: " + content.GetType().Name);
                                        NewLine(
                                            $"Unsupported content type: {content.GetType().Name}");
                                        AddStream(content.RawRepresentation?.ToString() ?? string.Empty);
                                        break;
                                }
                            }
                        }

                        chatHistory.AddRange(preResponseMessages);
                        responseMessages.AddRange(preResponseMessages);
                        loopUsageDetails ??= GetUsageDetailsFromAdditional(preResponse);
                        if (loopUsageDetails != null)
                        {
                            totalUsageDetails.Add(loopUsageDetails);
                        }

                        finishReason = preResponse.FinishReason ?? GetFinishReasonFromAdditional(preResponse);
                        if (finishReason == ChatFinishReason.ToolCalls)
                        {
                            logger?.LogInformation("Function call finished, need run function calls...");
                        }
                        else if (finishReason == ChatFinishReason.Stop)
                        {
                            logger?.LogInformation("Response completed without function calls.");
                            break;
                        }
                        else if (finishReason == ChatFinishReason.Length)
                        {
                            logger?.LogInformation("Exceeded maximum response length.");
                            break;
                        }
                        else if (finishReason == ChatFinishReason.ContentFilter)
                        {
                            logger?.LogWarning("Response was filtered by content filter.");
                            break;
                        }
                        else
                        {
                            logger?.LogWarning($"Unexpected finish reason: {finishReason}");
                        }

                        if (!functionCallEngine.TryParseFunctionCalls(preResponse, out var preFunctionCalls))
                        {
                            logger?.LogInformation("No function calls were requested, response completed.");
                            break;
                        }

                        if (kernelPluginCollection.Count == 0)
                        {
                            errorMessage =
                                $"No functions available to call. But {preFunctionCalls.Count} function calls were requested.";
                            break;
                        }

                        #region function call

                        NewLine("Processing function calls...");
                        var chatMessage = new ChatMessage();
                        chatHistory.Add(chatMessage);
                        responseMessages.Add(chatMessage);
                        var functionResultContents = new List<FunctionResultContent>();
                        foreach (var functionCallContent in preFunctionCalls)
                        {
                            if (!kernelPluginCollection.TryGetFunction(null, functionCallContent.Name,
                                    out var kernelFunction))
                            {
                                errorMessage =
                                    $"Function '{functionCallContent.Name}' not found, call failed. Procedure interrupted.";
                                break;
                            }

                            var additionalFunctionCallResults = chatContext.AdditionalFunctionCallResult;
                            //additionalFunctionCallResults 不能包含 FunctionResultContent
                            Trace.Assert(additionalFunctionCallResults.All(c => c is not FunctionResultContent));
                            var additionalUserMessageBuilder = chatContext.AdditionalUserMessage;
                            var chatMessageContents = chatMessage.Contents;
                            try
                            {
                                var arguments = new AIFunctionArguments(functionCallContent.Arguments);
                                additionalFunctionCallResults.Clear();
                                additionalUserMessageBuilder.Clear();
                                //调用拦截器
                                var invokeResult = await this.FunctionInterceptor.InvokeAsync(
                                    kernelFunction, arguments, functionCallContent, cancellationToken);
                                NewLine(
                                    $"Function '{functionCallContent.Name}' invoked successfully, result: {invokeResult}");
                                functionResultContents.Add(new FunctionResultContent(functionCallContent.CallId,
                                    invokeResult));
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
                                NewLine("Function call failed: " + e.Message);
                                chatMessageContents.Add(new FunctionResultContent(functionCallContent.CallId, null)
                                    { Exception = e });
                                if (IsQuitWhenFunctionCallFailed)
                                {
                                    errorMessage =
                                        $"Function '{functionCallContent.Name}' invocation failed: {e.Message}";
                                    break;
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
                    }
                    catch (Exception ex)
                    {
                        if (preUpdates.Count != 0)
                        {
                            responseMessages.AddRange(preUpdates.ToChatResponse().Messages);
                        }

                        errorMessage = ex.Message;
                        logger?.LogError("Error during response: {Exception}", ex);
                    }

                    if (errorMessage != null)
                    {
                        break;
                    }

                    NewLine();
                }
            }

            var duration = (int)(_stopwatch.ElapsedMilliseconds / 1000);
            var price = this.Model.PriceCalculator?.Calculate(totalUsageDetails);
            result.Usage = totalUsageDetails;
            result.ResponseMessages = responseMessages;
            result.ErrorMessage = errorMessage;
            result.Latency = latency ?? 0;
            result.Duration = duration;
            result.FinishReason = finishReason;
            result.Price = price;
            return result;
        }
        finally
        {
            IsResponding = false;
        }

        void NewLine(string? line = null)
        {
            stream?.Invoke(Environment.NewLine);
            if (!string.IsNullOrEmpty(line)) stream?.Invoke(line);
        }

        void AddStream(string text)
        {
            stream?.Invoke(text);
        }
    }

    private static ChatFinishReason? GetFinishReasonFromAdditional(ChatResponse? response)
    {
        if (response != null)
        {
            var messages = response.Messages;
            foreach (var message in messages)
            {
                var additionalProperties = message.AdditionalProperties;
                if (additionalProperties != null)
                {
                    if (additionalProperties.TryGetValue("FinishReason", out var finishReasonObj))
                    {
                        if (finishReasonObj is string finishReason)
                        {
                            return new ChatFinishReason(finishReason);
                        }
                    }
                }
            }
        }

        return null;
    }

    private static UsageDetails? GetUsageDetailsFromAdditional(ChatResponse? response)
    {
        UsageDetails? usageDetails = null;
        if (response != null)
        {
            var messages = response.Messages;
            foreach (var message in messages)
            {
                var additionalProperties = message.AdditionalProperties;
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
        }

        return usageDetails;
    }
}