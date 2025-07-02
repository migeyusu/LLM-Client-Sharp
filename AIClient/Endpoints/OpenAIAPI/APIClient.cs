using System.Net.Http;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.UI.Dialog;
using Microsoft.Extensions.AI;
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
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<APIModelInfo, IModelParams>(modelInfo, this.Parameters);
        _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };
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

    private readonly HttpClient _httpClient;

    private Kernel? _kernel;
    
    protected override IChatCompletionService CreateChatCompletionService()
    {
        var endpoint = new Uri(this._option.URL);
        var apiToken = _option.APIToken;
        var builder = Kernel.CreateBuilder();
        _kernel = builder
            .AddOpenAIChatCompletion(this.ModelInfo.Id, endpoint, apiToken, "LLMClient", "1.0.0", _httpClient)
            .Build();
        return _kernel.GetRequiredService<IChatCompletionService>();
    }

    protected override ChatOptions CreateChatOptions(IList<ChatMessage> messages)
    {
        var chatOptions = base.CreateChatOptions(messages);
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

#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001