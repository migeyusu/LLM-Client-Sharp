using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using AutoMapper;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMClient.Endpoints.Azure.Models;

#pragma warning disable SKEXP0001
public class AzureClientBase : LlmClientBase, ILLMClient
{
    private static readonly Mapper Mapper = new Mapper((new MapperConfiguration((expression =>
    {
        expression.CreateMap<AzureModelInfo, IModelParams>();
    }))));

    [JsonIgnore] public AzureModelInfo ModelInfo { get; }

    [JsonIgnore]
    public override string Name
    {
        get { return ModelInfo.FriendlyName; }
    }

    public override ILLMEndpoint Endpoint { get; }

    [JsonIgnore]
    public string Id
    {
        get { return ModelInfo.OriginalName; }
    }

    protected readonly AzureOption Option;

    [JsonIgnore]
    public override ILLMModel Model
    {
        get { return ModelInfo; }
    }

    public AzureClientBase(AzureEndPointBase endPoint, AzureModelInfo modelInfo)
    {
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<AzureModelInfo, IModelParams>(modelInfo, this.Parameters);
        Option = endPoint.Option;
        endPoint.PropertyChanged += EndPointOnPropertyChanged;
        _chatClient = CreateChatClient();
    }

    ~AzureClientBase()
    {
        ((AzureEndPointBase)this.Endpoint).PropertyChanged -= EndPointOnPropertyChanged;
    }

    private void EndPointOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _chatClient.Dispose();
        _chatClient = CreateChatClient();
    }

    // private readonly FieldInfo? _info = typeof(ChatCompletionsClient)
    //     .GetField("_apiVersion", BindingFlags.Instance | BindingFlags.NonPublic);

    private Kernel? _kernel;

    [Experimental("SKEXP0001")]
    private IChatClient CreateChatClient()
    {
        _kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(this.ModelInfo.Id, new Uri(Option.URL), this.Option.APIToken)
            .Build();
        return _kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
    }

    private IChatClient _chatClient;

    protected override IChatClient GetChatClient()
    {
        return _chatClient;
    }

    /*protected override IChatClient CreateChatClient()
    {
        var credential = new AzureKeyCredential(Option.APIToken);
        var chatCompletionsClient = new ChatCompletionsClient(new Uri(Option.URL), credential,
            new AzureAIInferenceClientOptions());
        _info?.SetValue(chatCompletionsClient, "2024-12-01-preview");
        return chatCompletionsClient.AsIChatClient();
    }*/
}