using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;

namespace LLMClient.Endpoints.OpenAIAPI;

[Obsolete("not support interval thinking")]
public class SKClient : OpenAIAPIClient
{
    public SKClient(APIEndPoint endPoint, APIModelInfo modelInfo, APIDefaultOption option, ILoggerFactory loggerFactory,
        ITokensCounter tokensCounter) : base(endPoint, modelInfo, option, loggerFactory, tokensCounter)
    {
    }
    
    private IChatClient CreateChatClient()
    {
        var apiToken = Option.APIToken;
        var apiUri = new Uri(Option.URL);
        HttpMessageHandler handler = Option.ProxySetting.GetRealProxy().CreateHandler();
        var additionalHttpHeader = Option.AdditionalHeaders;
        if (additionalHttpHeader != null && additionalHttpHeader.Count != 0)
        {
            handler = new AddtionalHandler(handler, additionalHttpHeader);
        }

        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        var openAiClient = new OpenAIClientEx(new ApiKeyCredential(apiToken), new OpenAIClientOptions()
        {
            Endpoint = apiUri,
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
            NetworkTimeout = Timeout.InfiniteTimeSpan
        }, Option.TreatNullChoicesAsEmptyResponse);
        var builder = Kernel.CreateBuilder();
#if DEBUG //只有debug模式下才需要获取每次请求的日志
        builder.Services.AddSingleton(LoggerFactory);
#endif
        var kernel = builder.AddOpenAIChatCompletion(this.Model.APIId, openAiClient)
            .Build();
        var chatClient = kernel.GetRequiredService<IChatCompletionService>().AsChatClient();
        return new ChatClientBuilder(chatClient)
            .UseLogging(LoggerFactory)
            .UseOpenTelemetry(LoggerFactory, sourceName: "OpenAIAPI",
                config => { config.EnableSensitiveData = true; })
            .Build();
    }
}
    
internal class AddtionalHandler : HttpMessageHandler
{
    public AddtionalHandler(HttpMessageHandler handler, Dictionary<string, string> additionalHttpHeader)
    {
        throw new NotImplementedException();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}