using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using AutoMapper;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Endpoints.OpenAIAPI;

#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public class APIClient : LlmClientBase
{
    private static readonly Mapper Mapper = new Mapper((new MapperConfiguration((expression =>
    {
        expression.CreateMap<APIModelInfo, IModelParams>();
    }))));

    public APIModelInfo ModelInfo { get; }

    public override ILLMModel Model
    {
        get { return ModelInfo; }
    }

    public override string Name
    {
        get { return ModelInfo.Name; }
    }

    public override ILLMEndpoint Endpoint { get; }

    private readonly APIDefaultOption _option;

    public APIClient(APIEndPoint endPoint, APIModelInfo modelInfo, APIDefaultOption option)
    {
        _option = option;
        option.PropertyChanged += OptionOnPropertyChanged;
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<APIModelInfo, IModelParams>(modelInfo, this.Parameters);
        EnsureKernel();
    }

    ~APIClient()
    {
        _option.PropertyChanged -= OptionOnPropertyChanged;
        _chatClient?.Dispose();
    }

    private void OptionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        EnsureKernel();
    }
    
    private Kernel? _kernel;

    private IChatClient? _chatClient;

    private void EnsureKernel()
    {
        var apiToken = _option.APIToken;
        var apiUri = new Uri(_option.URL);
        var httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };
        var openAiClient = new OpenAIClientEx(new ApiKeyCredential(apiToken), new OpenAIClientOptions()
        {
            Endpoint = apiUri,
            Transport = new HttpClientPipelineTransport(httpClient)
        });
        var builder = Kernel.CreateBuilder();
#if DEBUG
        var loggerFactory = ServiceLocator.GetService<ILoggerFactory>() ??
                            throw new ArgumentNullException("ServiceLocator.GetService<ILoggerFactory>()");
        builder.Services.AddSingleton(loggerFactory);
#endif

        _kernel = builder.AddOpenAIChatCompletion(this.Model.Id, openAiClient)
            .Build();
        _chatClient = _kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
        _chatClient?.Dispose();
    }

    protected override IChatClient GetChatClient()
    {
        if (_chatClient == null)
        {
            throw new NullReferenceException(
                "Chat client is not initialized. Ensure that the EnsureKernel method has been called.");
        }

        return _chatClient;
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages, IList<AITool>? tools = null,
        string? systemPrompt = null)
    {
        var chatOptions = base.CreateChatOptions(messages, tools, systemPrompt);
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