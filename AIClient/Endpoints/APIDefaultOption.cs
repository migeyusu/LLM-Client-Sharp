using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Component;
using OpenAI;

namespace LLMClient.Endpoints;

public class APIDefaultOption : BaseViewModel
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
    
    private bool _useGlobalProxy = true;
    public bool UseGlobalProxy
    {
        get => _useGlobalProxy;
        set
        {
            if (value == _useGlobalProxy) return;
            _useGlobalProxy = value;
            OnPropertyChanged();
        }
    }

    public ProxyOption ProxyOption { get; set; } = new ProxyOption();
    
    public OpenAIClientEx? OpenAIClient
    {
        get
        {
            if (string.IsNullOrEmpty(APIToken) || string.IsNullOrEmpty(URL))
            {
                return null;
            }

            var httpClient = new HttpClient(/*new DebugMessageLogger()*/) { Timeout = TimeSpan.FromMinutes(10) };
            return new OpenAIClientEx(new ApiKeyCredential(APIToken), new OpenAIClientOptions
            {
                Endpoint = new Uri(URL),
                Transport = new HttpClientPipelineTransport(httpClient)
            });
        }
    }
}