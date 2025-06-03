using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Azure;
using Azure.AI.Inference;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Plugins.Document;
using Microsoft.SemanticKernel.Plugins.Document.FileSystem;
using Microsoft.SemanticKernel.Plugins.Document.OpenXml;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.Endpoints;

public abstract class LlmClientBase : BaseViewModel, ILLMModelClient
{
    private bool _isResponding;
    public abstract string Name { get; }

    public abstract ILLMEndpoint Endpoint { get; }

    [JsonIgnore] public abstract ILLMModel Info { get; }

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

    [JsonIgnore] public virtual ObservableCollection<string> PreResponse { get; } = new ObservableCollection<string>();

    protected abstract IChatClient CreateChatClient();

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var modelInfo = this.Info;
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
    
    public virtual async Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default)
    {
        var cachedPreResponse = new StringBuilder();
        UsageDetails? usageDetails = null;
        try
        {
            PreResponse.Clear();
            cachedPreResponse.Clear();
            // PreResponse = "正在生成文档。。。。。";
            IsResponding = true;
            var messages = new List<ChatMessage>();
            var requestOptions = this.CreateChatOptions(messages);
            foreach (var dialogItem in dialogItems)
            {
                if (dialogItem.Message != null)
                {
                    messages.Add(dialogItem.Message);
                }
            }

            _stopwatch.Restart();
            int duration = 0;
            var chatClient = CreateChatClient();
            if (this.Parameters.Streaming)
            {
                string? errorMessage = null;
                int? latency = null;
                try
                {
                    AdditionalPropertiesDictionary? dictionary = null;
                    await foreach (var update in chatClient
                                       .GetStreamingResponseAsync(messages, requestOptions, cancellationToken))
                    {
                        if (!latency.HasValue)
                        {
                            latency = (int)_stopwatch.ElapsedMilliseconds;
                        }

                        if (update.AdditionalProperties != null)
                        {
                            dictionary = update.AdditionalProperties;
                        }

                        var updateContents = update.Contents;
                        foreach (var content in updateContents)
                        {
                            switch (content)
                            {
                                case UsageContent usageContent:
                                    usageDetails = usageContent.Details;
                                    break;
                                case TextContent textContent:
                                    PreResponse.Add(textContent.Text);
                                    cachedPreResponse.Append(textContent.Text);
                                    break;
                                default:
                                    Trace.Write("unsupported content");
                                    break;
                            }
                        }
                    }

                    duration = (int)(_stopwatch.ElapsedMilliseconds / 1000);
                    if (usageDetails == null && dictionary != null)
                    {
                        // 方法1: 检查 Metadata 中的 Usage 信息
                        if (dictionary.TryGetValue("Usage", out var usageObj))
                        {
                            if (usageObj is ChatTokenUsage chatTokenUsage)
                            {
                                usageDetails = new UsageDetails()
                                {
                                    InputTokenCount = chatTokenUsage.InputTokenCount,
                                    OutputTokenCount = chatTokenUsage.OutputTokenCount,
                                    TotalTokenCount = chatTokenUsage.TotalTokenCount,
                                };
                            }

                            /*if (usage.TryGetValue("TotalTokens", out var totalTokensObj))
                            {
                                // tokenCount = Convert.ToInt32(totalTokensObj);
                            }*/
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

                    if (usageDetails == null)
                    {
                        Trace.Write("usage details not provided");
                    }
                }
                catch (OperationCanceledException)
                {
                    errorMessage = "Operation was canceled.";
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                }

                return new CompletedResult(cachedPreResponse.Length == 0 ? null : cachedPreResponse.ToString(),
                    usageDetails ?? new UsageDetails())
                {
                    ErrorMessage = errorMessage,
                    Latency = latency ?? 0,
                    Duration = duration
                };
            }
            else
            {
                try
                {
                    var chatResponse =
                        await chatClient.GetResponseAsync(messages, requestOptions, cancellationToken);
                    duration = (int)(_stopwatch.ElapsedMilliseconds / 1000);
                    if (chatResponse.Usage == null)
                    {
                        if (chatResponse.AdditionalProperties?.TryGetValue("Usage", out var usageObj) == true)
                        {
                            if (usageObj is ChatTokenUsage chatTokenUsage)
                            {
                                usageDetails = new UsageDetails()
                                {
                                    InputTokenCount = chatTokenUsage.InputTokenCount,
                                    OutputTokenCount = chatTokenUsage.OutputTokenCount,
                                    TotalTokenCount = chatTokenUsage.TotalTokenCount,
                                };
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
                    }
                    else
                    {
                        usageDetails = chatResponse.Usage;
                    }

                    return new CompletedResult(chatResponse.Text, usageDetails ?? new UsageDetails())
                    {
                        Duration = duration
                    };
                }
                catch (Exception e)
                {
                    return new CompletedResult(null, new UsageDetails())
                    {
                        ErrorMessage = e.Message,
                        Duration = duration
                    };
                }
            }
        }
        finally
        {
            IsResponding = false;
        }
    }
}