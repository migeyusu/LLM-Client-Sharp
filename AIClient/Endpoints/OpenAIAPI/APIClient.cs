using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Endpoints.Converters;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using BinaryContent = System.ClientModel.BinaryContent;
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

    /*Kernel? _kernel = null;

    [Experimental("SKEXP0050")]
    private Kernel Kernel
    {
        get
        {
            if (_kernel == null)
            {
                var endpoint = new Uri(this._option.URL);
                var apiToken = _option.APIToken;
                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.Services.AddSingleton<IMemoryStore>(new VolatileMemoryStore())
                    .AddSingleton<ISemanticTextMemory, SemanticTextMemory>();
                _kernel = kernelBuilder.AddOpenAIChatCompletion(this.ModelInfo.Id, endpoint, apiToken)
                    .AddOpenAITextEmbeddingGeneration("text-embedding-v3",
                        new OpenAIClient(new ApiKeyCredential(apiToken),
                            new OpenAIClientOptions() { Endpoint = endpoint }))
                    .Build();
            }

            return _kernel;
        }
    }*/

    private Kernel? _kernel;

    private IChatClient? _chatClient;

    private void EnsureKernel()
    {
        var apiToken = _option.APIToken;
        var apiUri = new Uri(_option.URL);
        var httpClient = new HttpClient(new DebugMessageLogger()) { Timeout = TimeSpan.FromMinutes(10) };
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
            Debug.WriteLine(requestString);
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

public class OpenAIClientEx : OpenAIClient
{
    private readonly ApiKeyCredential _credential;
    private readonly OpenAIClientOptions _options;

    public OpenAIClientEx(ApiKeyCredential credential, OpenAIClientOptions options) : base(credential, options)
    {
        _credential = credential;
        _options = options;
    }

    public override ChatClient GetChatClient(string model)
    {
        return new OpenAIChatClientEx(model, _credential, this._options);
    }
}

public class OpenAIChatClientEx : ChatClient
{
    public OpenAIChatClientEx(string model, ApiKeyCredential credential, OpenAIClientOptions options)
        : base(model, credential, options)
    {
    }

    public override async Task<ClientResult> CompleteChatAsync(BinaryContent content, RequestOptions? options = null)
    {
        var clientContext = ChatContext<ClientContext>.Current;
        if (clientContext != null)
        {
            if (clientContext.AdditionalObjects.Count != 0)
            {
                await using (var oriStream = new MemoryStream())
                {
                    await content
                        .WriteToAsync(oriStream);
                    oriStream.Position = 0;
                    var jsonNode = await JsonNode.ParseAsync(oriStream);
                    if (jsonNode == null)
                    {
                        throw new InvalidOperationException("Content is not valid JSON.");
                    }

                    foreach (var additionalObject in clientContext.AdditionalObjects)
                    {
                        var node = JsonSerializer.SerializeToNode(additionalObject.Value);
                        jsonNode[additionalObject.Key] = node;
                    }

                    oriStream.SetLength(0);
                    await using (var writer = new Utf8JsonWriter(oriStream))
                    {
                        jsonNode.WriteTo(writer);
                        await writer.FlushAsync();
                    }

                    oriStream.Position = 0;
                    var modifiedData = await BinaryData.FromStreamAsync(oriStream);
                    content = BinaryContent.Create(modifiedData);
                }
            }
        }

        var result = await base.CompleteChatAsync(content, options);
        if (clientContext != null)
        {
            //may be streaming mode or not, streaming mode coordinate to a page
            clientContext.Result = result;
        }

        return result;
    }
}