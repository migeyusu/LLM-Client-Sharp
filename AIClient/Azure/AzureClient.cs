using System.Text;
using Azure;
using Azure.AI.Inference;

namespace LLMClient.Azure;

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

    public ILLMModel CurrentModel { get; set; }

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

    private readonly AzureKeyCredential _credential;

    private readonly ChatCompletionsClient _chatCompletionsClient;

    private readonly StringBuilder _cachedPreResponse = new StringBuilder();

    public AzureClient(string apiToken)
    {
        _credential = new AzureKeyCredential(apiToken);
        _chatCompletionsClient = new ChatCompletionsClient(
            new Uri("https://models.inference.ai.azure.com"),
            _credential, new AzureAIInferenceClientOptions());
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