using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LLMClient.Endpoints;

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

    public async IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var step = new ReactStep();

        // 启动后台生产
        var producerTask = Task.Run(async () =>
        {
            try
            {
                if (!string.IsNullOrEmpty(_fakeFilePath) && File.Exists(_fakeFilePath))
                {
                    var fakeResponse = await File.ReadAllTextAsync(_fakeFilePath, cancellationToken);
                    int next = Random.Shared.Next(8);
                    int index = 0;
                    while (index < fakeResponse.Length)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        var chunk = fakeResponse.Substring(index, Math.Min(next, fakeResponse.Length - index));
                        step.EmitText(chunk);
                        index += next;
                        next = Random.Shared.Next(8);
                        await Task.Delay(200, cancellationToken);
                    }
                }

                step.Complete(new StepResult
                {
                    Usage = new UsageDetails
                    {
                        InputTokenCount = 0,
                        OutputTokenCount = 0,
                        TotalTokenCount = 0,
                    },
                    FinishReason = ChatFinishReason.Stop,
                    IsCompleted = true,
                    Messages =
                    [
                        new ChatMessage(ChatRole.Assistant, "This is a fake response from NullLlmModelClient.")
                    ],
                });
            }
            catch (Exception ex)
            {
                step.CompleteWithError(ex);
            }
        }, cancellationToken);

        yield return step;
        await producerTask;
    }
}