using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Component;
using OpenAI;

namespace LLMClient.Endpoints;

public class APIDefaultOption : BaseViewModel<APIDefaultOption>
{
    private string _apiToken = string.Empty;

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

    private string _url = string.Empty;

    public string URL
    {
        get => _url;
        set
        {
            if (value == _url) return;
            _url = value;
            OnPropertyChanged();
        }
    }

    public ProxySetting ProxySetting { get; set; } = new ProxySetting();

    public OpenAIClientEx? CreateOpenAIClient()
    {
        if (string.IsNullOrEmpty(APIToken) || string.IsNullOrEmpty(URL))
        {
            return null;
        }

        var httpClient = new HttpClient( /*new DebugMessageLogger()*/) { Timeout = TimeSpan.FromMinutes(10) };
        return new OpenAIClientEx(new ApiKeyCredential(APIToken), new OpenAIClientOptions
        {
            Endpoint = new Uri(URL),
            Transport = new HttpClientPipelineTransport(httpClient)
        });
    }
}