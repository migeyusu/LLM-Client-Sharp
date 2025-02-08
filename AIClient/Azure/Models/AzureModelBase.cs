using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

namespace LLMClient.Azure.Models;

public class AzureModelBase : BaseViewModel, ILLMModel
{
    public AzureModelInfo ModelInfo { get; }

    public string Name
    {
        get { return ModelInfo.Name; }
    }

    public string? Id
    {
        get { return ModelInfo.OriginalName; }
    }

    public ImageSource? Icon
    {
        get { return UITheme.IsDarkMode ? ModelInfo.DarkModeIcon : ModelInfo.LightModeIcon; }
    }

    protected readonly AzureEndPoint Endpoint;

    private ObservableCollection<string> _preResponse = new ObservableCollection<string>();

    public object Info
    {
        get { return ModelInfo; }
    }

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

    ChatCompletionsClient Client
    {
        get
        {
            if (_client == null)
            {
                _client = new ChatCompletionsClient(new Uri(Endpoint.URL), _credential,
                    new AzureAIInferenceClientOptions());
                OnChatCompletionsClientChanged(_client);
            }

            return _client;
        }
    }

    private ChatCompletionsClient? _client;
    private readonly AzureKeyCredential _credential;

    public AzureModelBase(AzureEndPoint endpoint, AzureModelInfo modelInfo)
    {
        ModelInfo = modelInfo;
        UITheme.ModeChanged += UIThemeOnModeChanged;
        Endpoint = endpoint;
        endpoint.PropertyChanged += EndpointOnPropertyChanged;
        _credential = new AzureKeyCredential(endpoint.APIToken);
    }

    ~AzureModelBase()
    {
        UITheme.ModeChanged -= UIThemeOnModeChanged;
        Endpoint.PropertyChanged -= EndpointOnPropertyChanged;
    }

    protected virtual void OnChatCompletionsClientChanged(ChatCompletionsClient client)
    {
    }

    private void EndpointOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case "APIToken":
                _credential.Update(Endpoint.APIToken);
                break;
            case "URL":
                _client = new ChatCompletionsClient(new Uri(Endpoint.URL), _credential,
                    new AzureAIInferenceClientOptions());
                OnChatCompletionsClientChanged(_client);
                break;
        }
    }


    private void UIThemeOnModeChanged(bool obj)
    {
        this.OnPropertyChanged(nameof(Icon));
    }

    protected virtual ChatCompletionsOptions CreateChatOptions()
    {
        return new ChatCompletionsOptions()
        {
            Model = this.Id,
        };
    }

    public async Task<string> SendRequest(IEnumerable<DialogViewItem> dialogItems, string prompt,
        CancellationToken cancellationToken = default)
    {
        var cachedPreResponse = new StringBuilder();
        try
        {
            PreResponse.Clear();
            cachedPreResponse.Clear();
            // PreResponse = "正在生成文档。。。。。";
            IsResponsing = true;
            var requestOptions = this.CreateChatOptions();
            foreach (var dialogItem in dialogItems)
            {
                if (dialogItem.Message != null)
                {
                    requestOptions.Messages.Add(dialogItem.Message);
                }
            }

            using (var streamingResponse =
                   await Client.CompleteStreamingAsync(requestOptions, cancellationToken))
            {
                await foreach (var update in streamingResponse.EnumerateValues().WithCancellation(cancellationToken))
                {
                    var usage = update.Usage;
                    if (usage != null)
                    {
                        this.TotalTokens = usage.TotalTokens;
                        this.PromptTokens = usage.PromptTokens;
                        this.CompletionTokens = usage.CompletionTokens;
                    }

                    var updateContentUpdate = update.ContentUpdate;
                    cachedPreResponse.Append(updateContentUpdate);
                    PreResponse.Add(updateContentUpdate);
                }
            }

            return cachedPreResponse.ToString();
        }
        finally
        {
            IsResponsing = false;
        }
    }
}