using System.Text;
using Azure;
using Azure.AI.Inference;

namespace LLMClient;

public interface ILLMEndpoint
{
    ILLMClient CreateClient();
}

//为了尽可能抽象，要求单个方法就传递一次会话所需要的所有参数，防止文本生成、图像生成等任务类型的不相容
public interface ILLMClient
{
    bool IsResponsing { get; }
    string PreResponse { get; }

    Task<string> SendRequest(IEnumerable<DialogItem> dialogItems, string prompt, string? systemPrompt = null,
        CancellationToken cancellationToken = default);
}

public class AzureOption : BaseViewModel, ILLMEndpoint
{
    private string _apiToken = "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";

    public string APIToken
    {
        get => _apiToken;
        set
        {
            if (value == _apiToken) return;
            _apiToken = value;
            OnPropertyChanged();
        }
    }

    public ILLMClient CreateClient()
    {
        return new AzureClient(apiToken: _apiToken);
    }
}

public class AzureClient : BaseViewModel, ILLMClient
{
    private string _preResponse;

    public string PreResponse
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

    AzureKeyCredential credential;

    ChatCompletionsClient _chatCompletionsClient;

    private readonly StringBuilder _cachedPreResponse = new StringBuilder();

    public AzureClient(string apiToken)
    {
        credential = new AzureKeyCredential(apiToken);
        _chatCompletionsClient = new ChatCompletionsClient(
            new Uri("https://models.inference.ai.azure.com"),
            credential, new AzureAIInferenceClientOptions());
        _cachedPreResponse.Append("正在生成文档。。。。。");
    }

    public async Task<string> SendRequest(IEnumerable<DialogItem> dialogItems, string prompt,
        string? systemPromt = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _cachedPreResponse.Clear();
            _preResponse = string.Empty;
            OnPropertyChangedAsync(PreResponse);
            IsResponsing = true;
            var requestOptions = new ChatCompletionsOptions()
            {
                Model = "gpt-4o",
                Temperature = 1,
                MaxTokens = 4096,
            };
            if (!string.IsNullOrEmpty(systemPromt))
            {
                requestOptions.Messages.Add(new ChatRequestSystemMessage(systemPromt));
            }

            foreach (var dialogItem in dialogItems)
            {
                if (dialogItem.Message != null)
                {
                    requestOptions.Messages.Add(dialogItem.Message);
                }
            }

            requestOptions.Messages.Add(new ChatRequestUserMessage(prompt));
            using (var streamingResponse =
                   await _chatCompletionsClient.CompleteStreamingAsync(requestOptions, cancellationToken))
            {
                await foreach (var update in streamingResponse.EnumerateValues().WithCancellation(cancellationToken))
                {
                    _cachedPreResponse.Append(update.ContentUpdate);
                    PreResponse = _cachedPreResponse.ToString();
                }
            }

            return _cachedPreResponse.ToString();
        }
        finally
        {
            IsResponsing = false;
        }
    }
}