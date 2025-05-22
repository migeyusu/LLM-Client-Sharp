using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using AutoMapper;
using Azure;
using Azure.AI.Inference;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints.Azure.Models;

public class AzureClientBase : LlmClientBase, ILLMModelClient
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
    public override ILLMModel Info
    {
        get { return ModelInfo; }
    }

    public AzureClientBase(AzureEndPointBase endPoint, AzureModelInfo modelInfo)
    {
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<AzureModelInfo, IModelParams>(modelInfo, this.Parameters);
        Option = endPoint.Option;
    }

    ~AzureClientBase()
    {
    }
    
    private readonly FieldInfo? _info = typeof(ChatCompletionsClient)
        .GetField("_apiVersion", BindingFlags.Instance | BindingFlags.NonPublic);

    protected override IChatClient CreateChatClient()
    {
        var credential = new AzureKeyCredential(Option.APIToken);
        var chatCompletionsClient = new ChatCompletionsClient(new Uri(Option.URL), credential,
            new AzureAIInferenceClientOptions());
        _info?.SetValue(chatCompletionsClient, "2024-12-01-preview");
        return chatCompletionsClient.AsIChatClient();
        /*var build = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(this.ModelInfo.Id, new Uri(Option.URL), this.Option.APIToken)
            .Build();
        var chatCompletionService = build.GetRequiredService<IChatCompletionService>();
        return chatCompletionService.AsChatClient();*/
    }


#pragma warning disable SKEXP0010
    public static void Test()
    {
        /*Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "claude-3.5-sonnet",
                apiKey: "",
                endpoint: new Uri("https://api.individual.githubcopilot.com"))

            .Build();
        var invokePromptAsync = await kernel.InvokePromptAsync("hello");
        var s = invokePromptAsync.ToString();*/

        /*var credential = new AzureKeyCredential("");
        var chatCompletionsClient = new ChatCompletionsClient(new Uri("https://api.individual.githubcopilot.com"),
            credential,
            new AzureAIInferenceClientOptions());
        var completeAsync = await chatCompletionsClient.CompleteAsync(
            new ChatCompletionsOptions()
            {
                Model = "claude-3.5-sonne",
                Messages = [new ChatRequestUserMessage("您好")]
            });
        if (completeAsync.HasValue)
        {
            var valueContent = completeAsync.Value.Content;
        }*/
    }
#pragma warning restore SKEXP0010
}