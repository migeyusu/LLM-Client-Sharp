using LLMClient.Abstraction;
using LLMClient.Component;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Endpoints;

public class StubLlmClient : ILLMChatClient
{
    public IEndpointModel Model { get; } = StubLLMChatModel.Instance;
    public IModelParams Parameters { get; set; } = new DefaultModelParam();
    public string Name { get; } = "StubLlmClient";
    public bool IsResponding { get; set; }

    public async Task<CompletedResult> SendRequest(DialogContext context, IInvokeInteractor? interactor = null,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        interactor ??= new DebugInvokeInteractor();
#endif
        var str =
            "### 进程与命令（建议强权限控制）\n- `proc.run(command, args, cwd, timeout, env, capture)`：执行构建/测试/格式化等  \n  *建议带 allowlist 与沙箱策略；输出要可截断并带 exitCode。*";
        if (Parameters.Streaming)
        {
            var random = new Random();
            int currentIndex = 0;
            while (currentIndex < str.Length)
            {
                if (cancellationToken.IsCancellationRequested) break;

                int len = random.Next(3, 6); // 3 to 5 characters
                if (currentIndex + len > str.Length)
                {
                    len = str.Length - currentIndex;
                }

                var chunk = str.Substring(currentIndex, len);
                interactor?.Info(chunk);
                currentIndex += len;

                await Task.Delay(100, cancellationToken);
            }

            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails(),
                FinishReason = ChatFinishReason.Stop
            };
        }
        else
        {
            return new CompletedResult()
            {
                ResponseMessages = [new ChatMessage(ChatRole.Assistant, str)],
                Usage = new UsageDetails
                {
                    InputTokenCount = 0,
                    OutputTokenCount = 0,
                    TotalTokenCount = 0,
                    AdditionalCounts = null
                },
                Latency = 0,
                Duration = 0,
                ErrorMessage = null,
                Price = null,
                FinishReason = ChatFinishReason.Stop,
                Annotations = null,
                AdditionalProperties = null
            };
        }
    }

    public ILLMAPIEndpoint Endpoint { get; }
}

public class EmptyLlmModelClient : ILLMChatClient
{
    public static EmptyLlmModelClient Instance => new EmptyLlmModelClient();

    public string Name { get; } = "NullLlmModelClient";

    public ILLMAPIEndpoint Endpoint
    {
        get
        {
            return new APIEndPoint(new APIEndPointOption() { Name = "NullLlmModelClient" }, NullLoggerFactory.Instance);
        }
    }

    public IEndpointModel Model
    {
        get
        {
            return new APIModelInfo
            {
                APIId = "fake-model",
                Name = "Fake Model",
                IsNotMatchFromSource = false,
                Streaming = true,
                UrlIconEnable = false,
                IconType = ModelIconType.None,
                IconUrl = null,
                InfoUrl = "https://example.com/fake-model",
                Description = "This is a fake model for testing purposes.",
                MaxContextSize = 0,
                Endpoint = new EmptyLLMEndpoint(),
                SupportSystemPrompt = true,
                TopPEnable = true,
                TopKEnable = true,
                TemperatureEnable = true,
                MaxTokensEnable = true,
                FrequencyPenaltyEnable = true,
                PresencePenaltyEnable = true,
                SeedEnable = true,
                SystemPrompt = "默认系统提示",
                TopP = 0.9f,
                TopKMax = 100,
                TopK = 40,
                Temperature = 0.7f,
                MaxTokens = 2048,
                MaxTokenLimit = 4096,
                Reasonable = true,
                FunctionCallOnStreaming = true,
                SupportStreaming = true,
                SupportImageGeneration = true,
                SupportAudioGeneration = true,
                SupportVideoGeneration = true,
                SupportSearch = true,
                SupportFunctionCall = true,
                SupportAudioInput = true,
                SupportVideoInput = true,
                SupportTextGeneration = true,
                SupportImageInput = true,
                PriceCalculator = new TokenBasedPriceCalculator(),
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.5f,
                Seed = 42
            };
        }
    }

    public bool IsResponding { get; set; } = false;

    public IModelParams Parameters { get; set; } = new DefaultModelParam
    {
        Streaming = false,
        SystemPrompt = null,
        TopP = 0.9f,
        TopK = 40,
        Temperature = 0.7f,
        MaxTokens = 4096,
        FrequencyPenalty = 0.5f,
        PresencePenalty = 0.5f,
        Seed = 666
    };

    private readonly string? _fakeFilePath;

    public EmptyLlmModelClient(string? fakeFilePath = null)
    {
        this._fakeFilePath = fakeFilePath;
    }

    public async Task<CompletedResult> SendRequest(DialogContext context,
        IInvokeInteractor? stream = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_fakeFilePath))
        {
            if (File.Exists(_fakeFilePath))
            {
                var fakeResponse = await File.ReadAllTextAsync(_fakeFilePath, cancellationToken);
                int next = Random.Shared.Next(8);
                int index = 0;
                while (index < fakeResponse.Length)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var chunk = fakeResponse.Substring(index, Math.Min(next, fakeResponse.Length - index));
                    stream?.Info(chunk);
                    index += next;
                    next = Random.Shared.Next(8);
                    await Task.Delay(200, cancellationToken);
                }
            }
        }

        return new CompletedResult
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 0,
                OutputTokenCount = 0,
                TotalTokenCount = 0,
                AdditionalCounts = null
            },
            Latency = 0,
            Duration = 0,
            ErrorMessage = null,
            Price = null,
            FinishReason = ChatFinishReason.Stop,
            ResponseMessages =
            [
                new ChatMessage(ChatRole.Assistant, "This is a fake response from NullLlmModelClient.")
            ],
            Annotations = null,
            AdditionalProperties = null
        };
    }
}