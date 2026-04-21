#define REQUEST

using System.Net.Http;
using AutoMapper;
using Betalgo.Ranul.OpenAI;
using Betalgo.Ranul.OpenAI.Managers;
using LLMClient.Abstraction;
using LLMClient.Log;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;


namespace LLMClient.Endpoints.OpenAIAPI;

public class OpenAIAPIClient : LlmClientBase
{
    private static readonly Mapper Mapper =
        new(new MapperConfiguration(expression => { expression.CreateMap<APIModelInfo, IModelParams>(); },
            new NullLoggerFactory()));

    public APIModelInfo ModelInfo { get; }

    public override IEndpointModel Model
    {
        get { return ModelInfo; }
    }

    public override string Name
    {
        get { return ModelInfo.Name; }
    }

    public override ILLMAPIEndpoint Endpoint { get; }

    protected readonly APIDefaultOption Option;

    private APIDefaultOption _optionCopy;

    public OpenAIAPIClient(APIEndPoint endPoint, APIModelInfo modelInfo, APIDefaultOption option,
        ILoggerFactory loggerFactory, ITokensCounter tokensCounter) : base(tokensCounter)
    {
        Option = option;
        _optionCopy = Extension.Clone(option);
        this.LoggerFactory = loggerFactory;
        this.Endpoint = endPoint;
        ModelInfo = modelInfo;
        Mapper.Map<APIModelInfo, IModelParams>(modelInfo, this.Parameters);
    }

    ~OpenAIAPIClient()
    {
        _chatClient?.Dispose();
    }

    // private Kernel? _kernel;

    private IChatClient? _chatClient;

    protected readonly ILoggerFactory LoggerFactory;

    private const string UserAgentHeaderName = "User-Agent";

    private IChatClient EnsureCreate()
    {
        if (_chatClient != null)
        {
            _chatClient.Dispose();
        }

        var apiToken = Option.APIToken;
        HttpMessageHandler handler = Option.ProxySetting.GetRealProxy().CreateHandler();
#if DEBUG && REQUEST
        handler = new LoggingHandler(handler, LoggerFactory.CreateLogger<LoggingHandler>());
#endif
        var additionalHttpHeader = Option.AdditionalHeaders;
        try
        {
            var services = new ServiceCollection();
            var httpClient = new HttpClient(handler)
                { Timeout = TimeSpan.FromMinutes(10) };
            if (additionalHttpHeader != null && additionalHttpHeader.Count != 0)
            {
                var headers = httpClient.DefaultRequestHeaders;
                foreach (var (key, value) in additionalHttpHeader)
                {
                    if (key.Equals(UserAgentHeaderName, StringComparison.Ordinal))
                    {
                        headers.UserAgent.Clear();
                        headers.UserAgent.ParseAdd(value);
                    }
                    else
                    {
                        headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            var openAiService = new OpenAIService(new OpenAIOptions()
            {
                ApiKey = apiToken,
                BaseDomain = Option.URL,
            }, httpClient);
            services.AddSingleton<IChatClient>(openAiService);
            var serviceProvider = services.BuildServiceProvider();
            var chatClient = serviceProvider.GetRequiredService<IChatClient>();

            var protocolLogLoggerFactory = LoggerFactory.CreateLoggerFactoryWithProtocolLog();
            var builtClient = new ChatClientBuilder(chatClient)
                .UseLogging(protocolLogLoggerFactory)
                .UseOpenTelemetry(protocolLogLoggerFactory, sourceName: "OpenAIAPI",
                    config => { config.EnableSensitiveData = true; })
                .Build();

            return builtClient;
        }
        catch (Exception e)
        {
            var logger = LoggerFactory.CreateLogger<OpenAIAPIClient>();
            logger.LogError(e, "Failed to create OpenAI chat client.");
            throw;
        }
    }

    protected override IChatClient GetChatClient()
    {
        if (_chatClient == null || !Option.PublicEquals(_optionCopy))
        {
            _chatClient = EnsureCreate();
            _optionCopy = Extension.Clone(_optionCopy);
        }

        return _chatClient;
    }
}

public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public LoggingHandler(HttpMessageHandler innerHandler, ILogger logger)
        : base(innerHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🚀 [Outgoing Request] {Method} {Uri}",
            request.Method, request.RequestUri);

        _logger.LogInformation("📋 Headers: {Headers}",
            request.Headers.ToString()); // 会完整打印 User-Agent 等所有 header

        if (request.Content != null)
        {
            _logger.LogDebug("📦 Request Body (first 500 chars): {Body}",
                (await request.Content.ReadAsStringAsync(cancellationToken)).Substring(0,
                    Math.Min(500, (await request.Content.ReadAsStringAsync(cancellationToken)).Length)));
        }

        var response = await base.SendAsync(request, cancellationToken);

        _logger.LogInformation("📥 Response Status: {StatusCode}", response.StatusCode);
        return response;
    }
}