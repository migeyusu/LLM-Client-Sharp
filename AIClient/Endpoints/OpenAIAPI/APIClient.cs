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
        var httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };
        var builder = Kernel.CreateBuilder();
#if DEBUG
        var loggerFactory = ServiceLocator.GetService<ILoggerFactory>() ??
                            throw new ArgumentNullException("ServiceLocator.GetService<ILoggerFactory>()");
        builder.Services.AddSingleton(loggerFactory);
#endif
        _kernel = builder
            .AddOpenAIChatCompletion(this.ModelInfo.Id, new Uri(_option.URL), _option.APIToken, "LLMClient", "1.0.0",
                httpClient)
            .Build();
        _chatClient?.Dispose();
        _chatClient = _kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
    }

    protected override IChatClient GetChatClient()
    {
        return _chatClient!;
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages, string? systemPrompt = null)
    {
        var chatOptions = base.CreateChatOptions(messages, systemPrompt);
        /*chatOptions.AdditionalProperties = new AdditionalPropertiesDictionary(new Dictionary<string, object?>()
        {
            { "max_completion_tokens", this.ModelInfo.MaxTokens },
        });*/
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

public class SendMessageLogger : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestContent = request.Content;
        if (requestContent != null)
        {
            var stringAsync = await requestContent.ReadAsStringAsync(cancellationToken);
            Debug.WriteLine(stringAsync);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001