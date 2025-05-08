using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using AutoMapper;
using Azure;
using Azure.AI.Inference;
using LLMClient.Endpoints.OpenAIAPI;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using TextContent = Microsoft.Extensions.AI.TextContent;

namespace LLMClient.Endpoints.Azure.Models;

public class AzureClientBase : LlmClientBase, ILLMModelClient
{
    private static readonly Mapper Mapper = new Mapper((new MapperConfiguration((expression =>
    {
        // expression.AddMaps(typeof(AzureModelBase).Assembly);
        expression.CreateMap<AzureClientBase, DefaultModelParam>();
        expression.CreateMap<DefaultModelParam, AzureClientBase>();
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
        UITheme.ModeChanged += UIThemeOnModeChanged;
        Option = endPoint.Option;
    }

    ~AzureClientBase()
    {
        UITheme.ModeChanged -= UIThemeOnModeChanged;
    }

    public virtual IChatClient CreateClient(AzureOption endpoint)
    {
        var credential = new AzureKeyCredential(endpoint.APIToken);
        var chatCompletionsClient = new ChatCompletionsClient(new Uri(Option.URL), credential,
            new AzureAIInferenceClientOptions());
        return new AzureAIInferenceChatClient(chatCompletionsClient);
    }

    private void UIThemeOnModeChanged(bool obj)
    {
        this.OnPropertyChanged(nameof(Icon));
    }
    

    public override async Task<CompletedResult> SendRequest(IEnumerable<IDialogViewItem> dialogItems,
        CancellationToken cancellationToken = default)
    {
        var cachedPreResponse = new StringBuilder();
        try
        {
            PreResponse.Clear();
            cachedPreResponse.Clear();
            // PreResponse = "正在生成文档。。。。。";
            IsResponding = true;
            var messages = new List<ChatMessage>();
            var requestOptions = this.CreateChatOptions(messages);
            foreach (var dialogItem in dialogItems.Where((item => item.IsEnable)))
            {
                if (dialogItem.Message != null)
                {
                    messages.Add(dialogItem.Message);
                }
            }

            UsageDetails? usageDetails = null;
            await foreach (var update in CreateClient(Option)
                               .GetStreamingResponseAsync(messages, requestOptions, cancellationToken))
            {
                var updateContents = update.Contents;
                foreach (var content in updateContents)
                {
                    switch (content)
                    {
                        case UsageContent usageContent:
                            usageDetails = usageContent.Details;;
                            break;
                        case TextContent textContent:
                            PreResponse.Add(textContent.Text);
                            cachedPreResponse.Append(textContent.Text);
                            break;
                        default:
                            Trace.Write("unsupported content");
                            break;
                    }
                }
            }

            return new CompletedResult(cachedPreResponse.ToString(), usageDetails ?? new UsageDetails());
        }
        finally
        {
            IsResponding = false;
        }
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