using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Net.Http;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints.OpenAIAPI;

#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public class APIClient : LlmClientBase
{
    private static readonly Mapper Mapper =
        new(new MapperConfiguration(expression => { expression.CreateMap<APIModelInfo, IModelParams>(); },
            new NullLoggerFactory()));

    public APIModelInfo ModelInfo { get; }

    public override ILLMChatModel Model
    {
        get { return ModelInfo; }
    }

    public override string Name
    {
        get { return ModelInfo.Name; }
    }

    public override ILLMEndpoint Endpoint { get; }

    private readonly APIDefaultOption _option;

    private APIDefaultOption _optionCopy;

    public APIClient(APIEndPoint endPoint, APIModelInfo modelInfo, APIDefaultOption option,
        ILoggerFactory loggerFactory)
    {
        _option = option;
        _optionCopy = Extension.Clone(option);
        this._loggerFactory = loggerFactory;
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<APIModelInfo, IModelParams>(modelInfo, this.Parameters);
    }

    ~APIClient()
    {
        _chatClient?.Dispose();
    }

    // private Kernel? _kernel;

    private IChatClient? _chatClient;

    private readonly ILoggerFactory _loggerFactory;

    private IChatClient EnsureKernel()
    {
        if (_chatClient != null)
        {
            _chatClient.Dispose();
        }

        var apiToken = _option.APIToken;
        var apiUri = new Uri(_option.URL);
        var proxyOption = _option.ProxySetting.GetRealProxy();
        _proxySettingCopy = Extension.Clone(proxyOption);
        var handler = proxyOption.CreateHandler();
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        var openAiClient = new OpenAIClientEx(new ApiKeyCredential(apiToken), new OpenAIClientOptions()
        {
            Endpoint = apiUri,
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
            NetworkTimeout = Timeout.InfiniteTimeSpan
        });
        var builder = Kernel.CreateBuilder();
#if DEBUG //只有debug模式下才需要获取每次请求的日志
        builder.Services.AddSingleton(_loggerFactory);
#endif
        var kernel = builder.AddOpenAIChatCompletion(this.Model.Id, openAiClient)
            .Build();
        return kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
    }

    private ProxyOption? _proxySettingCopy;

    protected override IChatClient GetChatClient()
    {
        if (_chatClient == null || !_option.PublicEquals(_optionCopy) ||
            !_option.ProxySetting.GetRealProxy().PublicEquals(_proxySettingCopy!))
        {
            _chatClient = EnsureKernel();
            _optionCopy = Extension.Clone(_optionCopy);
        }

        return _chatClient;
    }

    protected override ChatOptions CreateChatOptions()
    {
        var chatOptions = base.CreateChatOptions();
        /*if (this.ModelInfo.ReasoningEnable)
        {
            chatOptions.AdditionalProperties = new AdditionalPropertiesDictionary(new Dictionary<string, object?>()
            {
                // { "reasoning_effort", ChatReasoningEffortLevel.Medium },
                {
                    "reasoning_effort", "medium"
                }
            });
        }*/
        return chatOptions;
    }
}

public class DebugMessageLogger : DelegatingHandler
{
    public DebugMessageLogger() : base(new HttpClientHandler())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestContent = request.Content;
        if (requestContent != null)
        {
            var requestString = await requestContent.ReadAsStringAsync(cancellationToken);
            /*var jsonNode = JsonNode.Parse(requestString);
            var foo = new[]
            {
                new
                {
                    id = "web",
                }
            };
            var node = JsonSerializer.SerializeToNode(foo);
            jsonNode["plugins"] = node;
            request.Content = new StringContent(JsonSerializer.Serialize(jsonNode));*/
        }

        var httpResponseMessage = await base.SendAsync(request, cancellationToken);
        var response = await httpResponseMessage.Content.ReadAsStringAsync(cancellationToken);
        Debug.WriteLine(response);
        return httpResponseMessage;
    }
}

#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001

public class CustomChatClient : DelegatingChatClient
{
    protected CustomChatClient(IChatClient innerClient) : base(innerClient)
    {
    }

    public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return base.GetResponseAsync(messages, options, cancellationToken);
    }
}