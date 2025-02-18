using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media;
using AutoMapper;
using Azure;
using Azure.AI.Inference;
using Azure.AI.TextAnalytics;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Text;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Azure.Models;

public class AzureModelBase : BaseViewModel, ILLMModel
{
    private static readonly Mapper Mapper = new Mapper((new MapperConfiguration((expression =>
    {
        // expression.AddMaps(typeof(AzureModelBase).Assembly);
        expression.CreateMap<AzureModelBase, AzureJsonModel>();
        expression.CreateMap<AzureTextModelBase, AzureJsonModel>();
        expression.CreateMap<OpenAIO1, AzureJsonModel>();
        expression.CreateMap<MetaLlama3, AzureJsonModel>();
        expression.CreateMap<DeepSeekR1, AzureJsonModel>();

        expression.CreateMap<AzureJsonModel, AzureModelBase>();
        expression.CreateMap<AzureJsonModel, AzureTextModelBase>();
        expression.CreateMap<AzureJsonModel, OpenAIO1>();
        expression.CreateMap<AzureJsonModel, MetaLlama3>();
        expression.CreateMap<AzureJsonModel, DeepSeekR1>();
    }))));

    [JsonIgnore] public AzureModelInfo ModelInfo { get; }

    public string Name
    {
        get { return ModelInfo.Name; }
    }

    public string Id
    {
        get { return ModelInfo.OriginalName; }
    }

    public ImageSource? Icon
    {
        get { return UITheme.IsDarkMode ? ModelInfo.DarkModeIcon : ModelInfo.LightModeIcon; }
    }

    public ChatMessage? Message { get; } = null;
    public bool IsEnable { get; } = false;

    protected readonly GithubCopilotEndPoint Endpoint;

    private ObservableCollection<string> _preResponse = new ObservableCollection<string>();

    public object Info
    {
        get { return ModelInfo; }
    }

    public virtual void Deserialize(IModelParams info)
    {
        Mapper.Map(info, this);
    }

    public virtual IModelParams Serialize()
    {
        var azureJsonModel = new AzureJsonModel();
        Mapper.Map(this, azureJsonModel);
        return azureJsonModel;
    }

    [JsonIgnore]
    public ObservableCollection<string> PreResponse
    {
        get => _preResponse;
        set
        {
            if (value == _preResponse) return;
            _preResponse = value;
            OnPropertyChangedAsync();
        }
    }

    private bool _isResponsing = false;

    [JsonIgnore]
    public bool IsResponsing
    {
        get => _isResponsing;
        set
        {
            if (value == _isResponsing) return;
            _isResponsing = value;
            OnPropertyChangedAsync();
        }
    }

    public int TotalTokens
    {
        get => _totalTokens;
        set
        {
            if (value == _totalTokens) return;
            _totalTokens = value;
            OnPropertyChanged();
        }
    }

    public int PromptTokens
    {
        get => _promptTokens;
        set
        {
            if (value == _promptTokens) return;
            _promptTokens = value;
            OnPropertyChanged();
        }
    }

    public int CompletionTokens
    {
        get => _completionTokens;
        set
        {
            if (value == _completionTokens) return;
            _completionTokens = value;
            OnPropertyChanged();
        }
    }

    private int _completionTokens;
    private int _promptTokens;
    private int _totalTokens;

    public virtual IChatClient CreateClient(GithubCopilotEndPoint endpoint)
    {
        var credential = new AzureKeyCredential(endpoint.APIToken);
        var chatCompletionsClient = new ChatCompletionsClient(new Uri(Endpoint.URL), credential,
            new AzureAIInferenceClientOptions());
        return new AzureAIInferenceChatClient(chatCompletionsClient);
    }

    public AzureModelBase(GithubCopilotEndPoint endpoint, AzureModelInfo modelInfo)
    {
        ModelInfo = modelInfo;
        UITheme.ModeChanged += UIThemeOnModeChanged;
        Endpoint = endpoint;
    }

    ~AzureModelBase()
    {
        UITheme.ModeChanged -= UIThemeOnModeChanged;
    }

    private void UIThemeOnModeChanged(bool obj)
    {
        this.OnPropertyChanged(nameof(Icon));
    }

    protected virtual ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        return new ChatOptions()
        {
            ModelId = this.Id,
        };
    }

    public virtual async Task<string> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default)
    {
        var cachedPreResponse = new StringBuilder();
        try
        {
            PreResponse.Clear();
            cachedPreResponse.Clear();
            // PreResponse = "正在生成文档。。。。。";
            IsResponsing = true;
            var messages = new List<ChatMessage>();
            var requestOptions = this.CreateChatOptions(messages);
            foreach (var dialogItem in dialogItems.Where((item => item.IsEnable)))
            {
                if (dialogItem.Message != null)
                {
                    messages.Add(dialogItem.Message);
                }
            }

            await foreach (var update in CreateClient(Endpoint)
                               .CompleteStreamingAsync(messages, requestOptions, cancellationToken))
            {
                var updateContents = update.Contents;
                foreach (var content in updateContents)
                {
                    switch (content)
                    {
                        case UsageContent usageContent:
                            var details = usageContent.Details;
                            this.TotalTokens = details.TotalTokenCount ?? 0;
                            this.PromptTokens = details.InputTokenCount ?? 0;
                            this.CompletionTokens = details.OutputTokenCount ?? 0;
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

            return cachedPreResponse.ToString();
        }
        finally
        {
            IsResponsing = false;
        }
    }
    
    public static async void Test()
    {
#pragma warning disable SKEXP0010
        
        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "claude-3.5-sonnet",
                apiKey: "",
                endpoint: new Uri("https://api.individual.githubcopilot.com"))
#pragma warning restore SKEXP0010
            .Build();
        var invokePromptAsync = await kernel.InvokePromptAsync("hello");
        var s = invokePromptAsync.ToString();
        
        /*var credential = new AzureKeyCredential("");
        var chatCompletionsClient = new ChatCompletionsClient(new Uri("https://api.individual.githubcopilot.com"),
            credential,
            new AzureAIInferenceClientOptions());
        var completeAsync = await chatCompletionsClient.CompleteAsync(
            new ChatCompletionsOptions()
            {
                Model = "claude-3.5-sonne",
                Messages = [new ChatRequestUserMessage("您好")]
            });
        if (completeAsync.HasValue)
        {
            var valueContent = completeAsync.Value.Content;
        }*/
    }
}