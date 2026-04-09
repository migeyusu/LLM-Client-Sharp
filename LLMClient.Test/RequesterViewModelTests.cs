using System.Collections.ObjectModel;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;

namespace LLMClient.Test;

public class RequesterViewModelTests
{
    [Fact]
    public void AvailableAgents_ContainsInspectAgent()
    {
        RunInSta(() =>
        {
            var requester = CreateRequester((_, _, _) => Task.FromResult<IResponse>(AgentTaskResult.Empty), string.Empty);

            Assert.Contains(requester.AvailableAgents, agent => agent.Type == typeof(InspectAgent));
        });
    }

    [Fact]
    public void Summarize_UsesSharedRequestPipeline_AndKeepsPromptText()
    {
        RunInSta(() =>
        {
            var requestSeen = new ManualResetEventSlim();
            var requestCompleted = new ManualResetEventSlim();
            var responseRelease = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            RequestOption? capturedOption = null;
            GetResponseHandler handler = async (option, _, _) =>
            {
                capturedOption = option;
                requestSeen.Set();
                return await responseRelease.Task;
            };
            var requester = CreateRequester(handler, "keep me");
            requester.IsAgentMode = false;
            requester.SelectedAgent = requester.AvailableAgents.First(agent => agent.Type == typeof(MiniSweAgent));
            requester.RequestCompleted += _ => requestCompleted.Set();

            requester.Summarize();

            Assert.True(requestSeen.Wait(TimeSpan.FromSeconds(5)));
            if (capturedOption == null)
            {
                throw new InvalidOperationException("Summary request option was not captured.");
            }

            var option = capturedOption;
            var summaryRequest = Assert.IsType<RequestViewItem>(option.RequestItem);
            Assert.True(requester.IsNewResponding);
            Assert.True(option.UseAgent);
            Assert.Equal(typeof(SummaryAgent), option.Agent?.Type);
            Assert.Same(requester.DefaultClient, option.DefaultClient);
            Assert.Equal(new GlobalOptions().ContextSummarizePrompt, summaryRequest.RawTextMessage);

            responseRelease.SetResult(new AgentTaskResult());

            Assert.True(requestCompleted.Wait(TimeSpan.FromSeconds(5)));
            WaitUntil(() => !requester.IsNewResponding, TimeSpan.FromSeconds(5));

            Assert.Equal("keep me", requester.PromptEditViewModel.FinalText);
            Assert.False(requester.IsNewResponding);
        });
    }

    [Fact]
    public void ComplexSummary_UsesSharedRequestPipeline_AndKeepsPromptText()
    {
        RunInSta(() =>
        {
            var requestSeen = new ManualResetEventSlim();
            var requestCompleted = new ManualResetEventSlim();
            var responseRelease = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            RequestOption? capturedOption = null;
            GetResponseHandler handler = async (option, _, _) =>
            {
                capturedOption = option;
                requestSeen.Set();
                return await responseRelease.Task;
            };
            var requester = CreateRequester(handler, "keep me");
            requester.IsAgentMode = false;
            requester.SelectedAgent = requester.AvailableAgents.First(agent => agent.Type == typeof(MiniSweAgent));
            requester.RequestCompleted += _ => requestCompleted.Set();

            requester.ComplexSummaryCommand.Execute(null);

            Assert.True(requestSeen.Wait(TimeSpan.FromSeconds(5)));
            if (capturedOption == null)
            {
                throw new InvalidOperationException("Complex summary request option was not captured.");
            }

            var option = capturedOption;
            var summaryRequest = Assert.IsType<RequestViewItem>(option.RequestItem);
            Assert.True(requester.IsNewResponding);
            Assert.True(option.UseAgent);
            Assert.Equal(typeof(SummaryAgent), option.Agent?.Type);
            Assert.Same(requester.DefaultClient, option.DefaultClient);
            Assert.Equal(new Summarizer(new GlobalOptions()).ConversationHistorySummaryPrompt, summaryRequest.RawTextMessage);

            responseRelease.SetResult(new AgentTaskResult());

            Assert.True(requestCompleted.Wait(TimeSpan.FromSeconds(5)));
            WaitUntil(() => !requester.IsNewResponding, TimeSpan.FromSeconds(5));

            Assert.Equal("keep me", requester.PromptEditViewModel.FinalText);
            Assert.False(requester.IsNewResponding);
        });
    }

    private static RequesterViewModel CreateRequester(GetResponseHandler handler, string initialPrompt)
    {
        return new RequesterViewModel(
            initialPrompt,
            EmptyLlmModelClient.Instance,
            handler,
            new GlobalOptions(),
            new Summarizer(new GlobalOptions()),
            new TestRagSourceCollection(),
            new TestTokensCounter());
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            Thread.Sleep(20);
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                completed.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
        if (exception != null)
        {
            throw new AggregateException(exception);
        }
    }

    private sealed class TestRagSourceCollection : IRagSourceCollection
    {
        public ObservableCollection<IRagSource> Sources { get; } = [];

        public Task LoadAsync()
        {
            return Task.CompletedTask;
        }

        public bool IsRunning => false;
    }

    private sealed class TestTokensCounter : ITokensCounter
    {
        public Task<long> CountTokens(string text)
        {
            return Task.FromResult((long)text.Length);
        }
    }
}

