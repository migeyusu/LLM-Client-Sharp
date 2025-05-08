using System.ComponentModel;
using System.Text;
using System.Windows.Media;
using AutoMapper;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using ImageSource = System.Windows.Media.ImageSource;
using TextContent = Microsoft.Extensions.AI.TextContent;
using Trace = System.Diagnostics.Trace;

namespace LLMClient.Endpoints.OpenAIAPI;

#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public class APIClient : LlmClientBase
{
    private readonly DefaultOption _option;

    private static readonly Mapper Mapper = new Mapper((new MapperConfiguration((expression =>
    {
        expression.CreateMap<DefaultModelParam, APIClient>();
        expression.CreateMap<APIClient, DefaultModelParam>();
        expression.CreateMap<APIModelInfo, APIClient>();
    }))));

    public static ImageSource IconImageSource => IconImageLazy.Value;

    private static readonly Lazy<ImageSource> IconImageLazy = new Lazy<ImageSource>((() =>
    {
        var packIcon = new PackIcon() { Kind = PackIconKind.Api };
        var packIconData = packIcon.Data;
        var geometry = Geometry.Parse(packIconData);
        var drawingImage =
            new DrawingImage(new GeometryDrawing(Brushes.Black, new Pen(Brushes.White, 0), geometry));
        drawingImage.Freeze();
        return drawingImage;
    }));

    public bool Streaming
    {
        get => _streaming;
        set
        {
            if (value == _streaming) return;
            _streaming = value;
            OnPropertyChanged();
        }
    }

    public APIModelInfo ModelInfo { get; }

    public override ILLMModel? Info
    {
        get { return ModelInfo; }
    }


    public override string Name
    {
        get { return ModelInfo.Name; }
    }

    public override ILLMEndpoint Endpoint { get; }

    public override ImageSource? Icon
    {
        get { return ModelInfo.Icon; }
    }

    public string? SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
        }
    }

    public float TopP
    {
        get => _topP;
        set
        {
            if (value.Equals(_topP)) return;
            _topP = value;
            OnPropertyChanged();
        }
    }

    public int TopK
    {
        get => _topK;
        set
        {
            if (value == _topK) return;
            _topK = value;
            OnPropertyChanged();
        }
    }


    public float Temperature
    {
        get => _temperature;
        set
        {
            if (value.Equals(_temperature)) return;
            _temperature = value;
            OnPropertyChanged();
        }
    }

    public int MaxTokens
    {
        get => _maxTokens;
        set
        {
            if (value == _maxTokens) return;
            _maxTokens = value;
            OnPropertyChanged();
        }
    }

    public float FrequencyPenalty
    {
        get => _frequencyPenalty;
        set
        {
            if (value.Equals(_frequencyPenalty)) return;
            _frequencyPenalty = value;
            OnPropertyChanged();
        }
    }

    public float PresencePenalty
    {
        get => _presencePenalty;
        set
        {
            if (value.Equals(_presencePenalty)) return;
            _presencePenalty = value;
            OnPropertyChanged();
        }
    }

    public long? Seed
    {
        get => _seed;
        set
        {
            if (value == _seed) return;
            _seed = value;
            OnPropertyChanged();
        }
    }

    private string? _systemPrompt;

    private float _topP;

    private int _topK;
    private float _frequencyPenalty;
    private float _presencePenalty;
    private long? _seed;
    private float _temperature;
    private int _maxTokens;

    private IChatClient _chatClient;

    private bool _streaming = true;

    public APIClient(APIEndPoint endPoint, APIModelInfo modelInfo, DefaultOption option)
    {
        _option = option;
        _option.PropertyChanged += OptionOnPropertyChanged;
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        modelInfo.PropertyChanged += ModelInfoOnPropertyChanged;
        _chatClient = CreateChatClient(modelInfo, option);
        Mapper.Map<APIModelInfo, APIClient>(modelInfo, this);
    }

    private void ModelInfoOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ModelInfo.Icon):
                this.OnPropertyChanged(nameof(Icon));
                break;
        }
    }

    ~APIClient()
    {
        _option.PropertyChanged -= OptionOnPropertyChanged;
        ModelInfo.PropertyChanged -= ModelInfoOnPropertyChanged;
    }

    private IChatClient CreateChatClient(APIModelInfo modelInfo, DefaultOption option)
    {
        /*var chatCompletionsClient = new ChatCompletionsClient(new Uri(option.URL),
            DelegatedTokenCredential.Create(((context, token) =>
                new AccessToken(option.APIToken, DateTimeOffset.MaxValue))));
        return chatCompletionsClient.AsChatClient();*/
        var build = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelInfo.Id, new Uri(option.URL), option.APIToken)
            .Build();
        var chatCompletionService = build.GetRequiredService<IChatCompletionService>();
        return chatCompletionService.AsChatClient();
    }

    private void OptionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _chatClient = CreateChatClient(ModelInfo, _option);
        /*switch (e.PropertyName)
        {
            case nameof(DefaultOption.APIToken):
                break;
            case nameof(DefaultOption.URL):
                break;
        }*/
    }

    public override void Deserialize(IModelParams info)
    {
        Mapper.Map(info, this);
    }

    public override IModelParams Serialize()
    {
        var defaultModelParam = new DefaultModelParam();
        Mapper.Map(this, defaultModelParam);
        return defaultModelParam;
    }

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        if (ModelInfo.SystemPromptEnable && !string.IsNullOrWhiteSpace(SystemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        }

        var chatOptions = new ChatOptions()
        {
            ModelId = this.ModelInfo.Id,
        };
        if (ModelInfo.TopPEnable)
        {
            chatOptions.TopP = this.TopP;
        }

        if (ModelInfo.TopKEnable)
        {
            chatOptions.TopK = this.TopK;
        }

        if (ModelInfo.TemperatureEnable)
        {
            chatOptions.Temperature = this.Temperature;
        }

        if (ModelInfo.MaxTokensEnable)
        {
            chatOptions.MaxOutputTokens = this.MaxTokens;
        }

        if (ModelInfo.FrequencyPenaltyEnable)
        {
            chatOptions.FrequencyPenalty = this.FrequencyPenalty;
        }

        if (ModelInfo.PresencePenaltyEnable)
        {
            chatOptions.PresencePenalty = this.PresencePenalty;
        }

        if (ModelInfo.SeedEnable && this.Seed.HasValue)
        {
            chatOptions.Seed = this.Seed.Value;
        }

        return chatOptions;
    }

    public override async Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default)
    {
        var cachedPreResponse = new StringBuilder();
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

            if (Streaming)
            {
                AdditionalPropertiesDictionary? dictionary = null;
                UsageDetails? usageDetails = null;
                await foreach (var update in _chatClient
                                   .GetStreamingResponseAsync(messages, requestOptions, cancellationToken))
                {
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
                else
                {
                    this.TokensConsumption += usageDetails.TotalTokenCount ?? 0;
                }

                return new CompletedResult(cachedPreResponse.ToString(), usageDetails ?? new UsageDetails());
            }
            else
            {
                UsageDetails? usageDetails = null;
                var chatResponse = await _chatClient.GetResponseAsync(messages, requestOptions, cancellationToken);
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
                }
                else
                {
                    usageDetails = chatResponse.Usage;
                }

                return new CompletedResult(chatResponse.Text, usageDetails ?? new UsageDetails());
            }
        }
        finally
        {
            IsResponding = false;
        }
    }


    /*private ChatCompletionsOptions ToAzureAIOptions(
      IEnumerable<ChatMessage> chatContents,
      ChatOptions? options)
    {
      ChatCompletionsOptions completionsOptions = new ChatCompletionsOptions(this.ToAzureAIInferenceChatMessages(chatContents));
      string str1 = options?.ModelId;
      if (str1 == null)
        str1 = this._metadata.ModelId ?? throw new InvalidOperationException("No model id was provided when either constructing the client or in the chat options.");
      completionsOptions.Model = str1;
      ChatCompletionsOptions azureAiOptions = completionsOptions;
      if (options != null)
      {
        azureAiOptions.FrequencyPenalty = options.FrequencyPenalty;
        azureAiOptions.MaxTokens = options.MaxOutputTokens;
        azureAiOptions.NucleusSamplingFactor = options.TopP;
        azureAiOptions.PresencePenalty = options.PresencePenalty;
        azureAiOptions.Temperature = options.Temperature;
        azureAiOptions.Seed = options.Seed;
        IList<string> stopSequences = options.StopSequences;
        if (stopSequences != null && stopSequences.Count > 0)
        {
          foreach (string str2 in (IEnumerable<string>) stopSequences)
            azureAiOptions.StopSequences.Add(str2);
        }
        int? topK = options.TopK;
        if (topK.HasValue)
        {
          int valueOrDefault = topK.GetValueOrDefault();
          azureAiOptions.AdditionalProperties["top_k"] = new BinaryData(JsonSerializer.SerializeToUtf8Bytes((object) valueOrDefault, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof (int))));
        }
        AdditionalPropertiesDictionary additionalProperties = options.AdditionalProperties;
        if (additionalProperties != null)
        {
          foreach (KeyValuePair<string, object> keyValuePair in (AdditionalPropertiesDictionary<object>) additionalProperties)
          {
            string key = keyValuePair.Key;
            if (keyValuePair.Value != null)
            {
              byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(keyValuePair.Value, this.ToolCallJsonSerializerOptions.GetTypeInfo(typeof (object)));
              azureAiOptions.AdditionalProperties[keyValuePair.Key] = new BinaryData(utf8Bytes);
            }
          }
        }
        IList<AITool> tools = options.Tools;
        if (tools != null && tools.Count > 0)
        {
          foreach (AITool aiTool in (IEnumerable<AITool>) tools)
          {
            if (aiTool is AIFunction aiFunction)
              azureAiOptions.Tools.Add(AzureAIInferenceChatClient.ToAzureAIChatTool(aiFunction));
          }
          switch (options.ToolMode)
          {
            case NoneChatToolMode _:
              azureAiOptions.ToolChoice = ChatCompletionsToolChoice.None;
              break;
            case AutoChatToolMode _:
            case null:
              azureAiOptions.ToolChoice = ChatCompletionsToolChoice.Auto;
              break;
            case RequiredChatToolMode requiredChatToolMode:
              azureAiOptions.ToolChoice = requiredChatToolMode.RequiredFunctionName == null ? ChatCompletionsToolChoice.Required : new ChatCompletionsToolChoice(new FunctionDefinition(requiredChatToolMode.RequiredFunctionName));
              break;
          }
        }
        if (options.ResponseFormat is ChatResponseFormatText)
          azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateTextFormat();
        else if (options.ResponseFormat is ChatResponseFormatJson responseFormat)
        {
          JsonElement? schema = responseFormat.Schema;
          if (schema.HasValue)
          {
            AzureAIChatToolJson azureAiChatToolJson = schema.GetValueOrDefault().Deserialize<AzureAIChatToolJson>(JsonContext.Default.AzureAIChatToolJson);
            azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateJsonFormat(responseFormat.SchemaName ?? "json_schema", (IDictionary<string, BinaryData>) new Dictionary<string, BinaryData>()
            {
              ["type"] = AzureAIInferenceChatClient._objectString,
              ["properties"] = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes<Dictionary<string, JsonElement>>(azureAiChatToolJson.Properties, JsonContext.Default.DictionaryStringJsonElement)),
              ["required"] = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes<List<string>>(azureAiChatToolJson.Required, JsonContext.Default.ListString)),
              ["additionalProperties"] = AzureAIInferenceChatClient._falseString
            }, responseFormat.SchemaDescription, new bool?());
          }
          else
            azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateJsonFormat();
        }
      }
      return azureAiOptions;
    }*/
}
#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001