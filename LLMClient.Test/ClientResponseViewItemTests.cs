using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;

namespace LLMClient.Test;

public class ClientResponseViewItemTests
{
    [Fact]
    public void ParallelResponseViewItem_RaisesIsResponding_WhenChildRespondingChanges()
    {
        var session = new PreviewBlockingDialogSessionViewModel();
        var parallelResponseViewItem = new ParallelResponseViewItem(session);
        var responseViewItem = new ClientResponseViewItem(new NeverInvokedChatClient());
        var changedProperties = new List<string?>();
        parallelResponseViewItem.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        parallelResponseViewItem.AddResponse(responseViewItem);
        changedProperties.Clear();

        responseViewItem.AcquireRespondingState();

        Assert.True(parallelResponseViewItem.IsResponding);
        Assert.Contains(nameof(ParallelResponseViewItem.IsResponding), changedProperties);

        changedProperties.Clear();
        responseViewItem.ReleaseRespondingState();

        Assert.False(parallelResponseViewItem.IsResponding);
        Assert.Contains(nameof(ParallelResponseViewItem.IsResponding), changedProperties);
    }

    [Fact]
    public async Task ParallelResponseViewItem_NewResponse_SetsClientResponseResponding_DuringPreviewProcessing()
    {
        var session = new PreviewBlockingDialogSessionViewModel();
        var parallelResponseViewItem = new ParallelResponseViewItem(session);
        var client = new NeverInvokedChatClient();
        using var cancellationTokenSource = new CancellationTokenSource();

        var processingTask = parallelResponseViewItem.NewResponse(client, cancellationTokenSource.Token);

        await session.PreviewStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var responseViewItem = Assert.Single(parallelResponseViewItem.Items);
        Assert.True(responseViewItem.IsResponding);
        Assert.Equal(1, session.RespondingCount);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await processingTask.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.False(responseViewItem.IsResponding);
        Assert.Equal(0, session.RespondingCount);
    }

    [Fact]
    public void ClientResponseViewItem_ContextUsage_UsesLastSuccessfulInputTokensAgainstModelMaxContext()
    {
        var client = new NeverInvokedChatClient(128_000);
        var responseViewItem = new ClientResponseViewItem(client)
        {
            LastSuccessfulUsage = new UsageDetails
            {
                InputTokenCount = 32_000,
                OutputTokenCount = 2_000,
                TotalTokenCount = 34_000,
            }
        };

        var usageViewModel = responseViewItem.ContextUsage;
        Assert.True(usageViewModel.HasContextUsage);
        Assert.Equal(32_000, usageViewModel.ContextUsageTokenCount);
        Assert.Equal(128_000, usageViewModel.MaxContextTokens);
        Assert.Equal(0.25d, usageViewModel.ContextUsageRatio);
        Assert.Equal(25d, usageViewModel.ContextUsagePercent);
        Assert.False(usageViewModel.IsContextUsageWarning);
        Assert.False(usageViewModel.IsContextUsageCritical);
        Assert.Contains("25%", usageViewModel.ContextUsageSummary);
        Assert.Contains("32k", usageViewModel.ContextUsageSummary);
        Assert.Contains("128k", usageViewModel.ContextUsageSummary);
    }

    private sealed class PreviewBlockingDialogSessionViewModel : DialogSessionViewModel
    {
        public PreviewBlockingDialogSessionViewModel() : base(new GlobalOptions(), new Summarizer(new GlobalOptions()),
            null)
        {
        }

        public TaskCompletionSource<bool> PreviewStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string? SystemPrompt => null;

        public override async Task OnPreviewRequest(CancellationToken token)
        {
            PreviewStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
    }

    private sealed class NeverInvokedChatClient : ILLMChatClient
    {
        public NeverInvokedChatClient(int maxContextSize = 200 * 1000)
        {
            Model = new APIModelInfo
            {
                APIId = "preview-only-model",
                Name = "Preview Only Model",
                Endpoint = EmptyLLMEndpoint.Instance,
                MaxContextSize = maxContextSize,
                SupportFunctionCall = false,
                SupportStreaming = false,
                SupportSystemPrompt = true,
                FunctionCallOnStreaming = false,
                SupportTextGeneration = true,
                TopPEnable = true,
                TopKEnable = true,
                TemperatureEnable = true,
                MaxTokensEnable = true,
                FrequencyPenaltyEnable = true,
                PresencePenaltyEnable = true,
                SeedEnable = true,
                PriceCalculator = new TokenBasedPriceCalculator()
            };
        }

        public string Name => "NeverInvokedChatClient";

        public ILLMAPIEndpoint Endpoint => EmptyLLMEndpoint.Instance;

        public IEndpointModel Model { get; }

        public IModelParams Parameters { get; set; } = new DefaultModelParam { Streaming = false };

        public bool IsResponding { get; set; }

        public IAsyncEnumerable<ReactStep> SendRequestAsync(IRequestContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "SendRequest should not be called while preview processing is blocked.");
        }
    }
}