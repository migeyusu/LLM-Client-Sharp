using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows.Media;
using AutoMapper;
using Azure.AI.Inference;
using LLMClient.Abstraction;
using LLMClient.UI;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Document;
using Microsoft.SemanticKernel.Plugins.Document.FileSystem;
using Microsoft.SemanticKernel.Plugins.Document.OpenXml;
using OpenAI;
using OpenAI.Chat;
using OpenAI.VectorStores;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;
using ImageSource = System.Windows.Media.ImageSource;
using TextContent = Microsoft.Extensions.AI.TextContent;
using Trace = System.Diagnostics.Trace;

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

    public override ILLMModel Info
    {
        get { return ModelInfo; }
    }


    public override string Name
    {
        get { return ModelInfo.Name; }
    }

    public override ILLMEndpoint Endpoint { get; }

    private readonly DefaultOption _option;


    public APIClient(APIEndPoint endPoint, APIModelInfo modelInfo, DefaultOption option)
    {
        _option = option;
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<APIModelInfo, IModelParams>(modelInfo, this.Parameters);
    }

    ~APIClient()
    {
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

    HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

    [Experimental("SKEXP0050")]
    protected override IChatClient CreateChatClient()
    {
        /*var chatCompletionsClient = new ChatCompletionsClient(new Uri(option.URL),
              DelegatedTokenCredential.Create(((context, token) =>
                  new AccessToken(option.APIToken, DateTimeOffset.MaxValue))));
          return chatCompletionsClient.AsChatClient();*/
        var endpoint = new Uri(this._option.URL);
        var apiToken = _option.APIToken;
        var builder = Kernel.CreateBuilder();
        var kernel = builder
            .AddOpenAIChatCompletion(this.ModelInfo.Id, endpoint, apiToken, "LLMClient", "1.0.0", httpClient)
            .Build();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        return chatCompletionService.AsChatClient();
    }


    /*private ChatCompletionsOptions ToAzureAIOptions(
      IEnumerable<ChatMessage> chatContents,
      ChatOptions? options)
    {
      ChatCompletionsOptions completionsOptions = new ChatCompletionsOptions(this.ToAzureAIInferenceChatMessages(chatContents));
      string str1 = options?.ModelId;
      if (str1 == null)
        str1 = this._metadata.ModelId ?? throw new InvalidOperationException("No model id was provided when either constructing the client or in the chat options.");
      completionsOptions.Model = str1;
      ChatCompletionsOptions azureAiOptions = completionsOptions;
      if (options != null)
      {
        azureAiOptions.FrequencyPenalty = options.FrequencyPenalty;
        azureAiOptions.MaxTokens = options.MaxOutputTokens;
        azureAiOptions.NucleusSamplingFactor = options.TopP;
        azureAiOptions.PresencePenalty = options.PresencePenalty;
        azureAiOptions.Temperature = options.Temperature;
        azureAiOptions.Seed = options.Seed;
        IList<string> stopSequences = options.StopSequences;
        if (stopSequences != null && stopSequences.Count > 0)
        {
          foreach (string str2 in (IEnumerable<string>) stopSequences)
            azureAiOptions.StopSequences.Add(str2);
        }
        int? topK = options.TopK;
        if (topK.HasValue)
        {
          int valueOrDefault = topK.GetValueOrDefault();
          azureAiOptions.AdditionalProperties["top_k"] = new BinaryData(JsonSerializer.SerializeToUtf8Bytes((object) valueOrDefault, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof (int))));
        }
        AdditionalPropertiesDictionary additionalProperties = options.AdditionalProperties;
        if (additionalProperties != null)
        {
          foreach (KeyValuePair<string, object> keyValuePair in (AdditionalPropertiesDictionary<object>) additionalProperties)
          {
            string key = keyValuePair.Key;
            if (keyValuePair.Value != null)
            {
              byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(keyValuePair.Value, this.ToolCallJsonSerializerOptions.GetTypeInfo(typeof (object)));
              azureAiOptions.AdditionalProperties[keyValuePair.Key] = new BinaryData(utf8Bytes);
            }
          }
        }
        IList<AITool> tools = options.Tools;
        if (tools != null && tools.Count > 0)
        {
          foreach (AITool aiTool in (IEnumerable<AITool>) tools)
          {
            if (aiTool is AIFunction aiFunction)
              azureAiOptions.Tools.Add(AzureAIInferenceChatClient.ToAzureAIChatTool(aiFunction));
          }
          switch (options.ToolMode)
          {
            case NoneChatToolMode _:
              azureAiOptions.ToolChoice = ChatCompletionsToolChoice.None;
              break;
            case AutoChatToolMode _:
            case null:
              azureAiOptions.ToolChoice = ChatCompletionsToolChoice.Auto;
              break;
            case RequiredChatToolMode requiredChatToolMode:
              azureAiOptions.ToolChoice = requiredChatToolMode.RequiredFunctionName == null ? ChatCompletionsToolChoice.Required : new ChatCompletionsToolChoice(new FunctionDefinition(requiredChatToolMode.RequiredFunctionName));
              break;
          }
        }
        if (options.ResponseFormat is ChatResponseFormatText)
          azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateTextFormat();
        else if (options.ResponseFormat is ChatResponseFormatJson responseFormat)
        {
          JsonElement? schema = responseFormat.Schema;
          if (schema.HasValue)
          {
            AzureAIChatToolJson azureAiChatToolJson = schema.GetValueOrDefault().Deserialize<AzureAIChatToolJson>(JsonContext.Default.AzureAIChatToolJson);
            azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateJsonFormat(responseFormat.SchemaName ?? "json_schema", (IDictionary<string, BinaryData>) new Dictionary<string, BinaryData>()
            {
              ["type"] = AzureAIInferenceChatClient._objectString,
              ["properties"] = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes<Dictionary<string, JsonElement>>(azureAiChatToolJson.Properties, JsonContext.Default.DictionaryStringJsonElement)),
              ["required"] = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes<List<string>>(azureAiChatToolJson.Required, JsonContext.Default.ListString)),
              ["additionalProperties"] = AzureAIInferenceChatClient._falseString
            }, responseFormat.SchemaDescription, new bool?());
          }
          else
            azureAiOptions.ResponseFormat = ChatCompletionsResponseFormat.CreateJsonFormat();
        }
      }
      return azureAiOptions;
    }*/
}
#pragma warning restore SKEXP0010
#pragma warning restore SKEXP0001