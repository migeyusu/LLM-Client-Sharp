using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Threading;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel.Base;
using Markdig;

namespace LLMClient.Test;

public class DispatcherRegressionTests
{
    [Fact]
    public void BaseViewModel_Dispatch_FaultedUiWork_IsObserved()
    {
        var trace = RunObservedDispatcherScenario(_ =>
        {
            var viewModel = new DispatchProbeViewModel();
            viewModel.InitializeDispatcher();

            var worker = new Thread(() =>
                viewModel.Post(() => throw new InvalidOperationException("dispatch boom")));
            worker.Start();
            worker.Join();
        });

        Assert.Contains(trace.Messages, message =>
            message.Contains(nameof(DispatchProbeViewModel), StringComparison.Ordinal) &&
            message.Contains("dispatch boom", StringComparison.Ordinal));
    }

    [Fact]
    public void StreamingRenderSession_OnBlockClosed_FaultedUiWork_IsObserved()
    {
        var trace = RunObservedDispatcherScenario(_ =>
        {
            var document = new FlowDocument();
            var session = new StreamingRenderSession(document,
                () => throw new InvalidOperationException("clear tail boom"));
            try
            {
                var markdown = Markdown.Parse("tool output", CustomMarkdownRenderer.DefaultPipeline);
                var block = markdown[0];
                session.OnBlockClosed(block);
            }
            finally
            {
                session.Dispose();
            }
        });

        Assert.Contains(trace.Messages, message =>
            message.Contains($"{nameof(StreamingRenderSession)}.{nameof(StreamingRenderSession.OnBlockClosed)} failed:",
                StringComparison.Ordinal));
    }

    private static CollectingTraceListener RunObservedDispatcherScenario(Action<Dispatcher> scenario)
    {
        Exception? unobserved = null;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            unobserved = args.Exception;
            args.SetObserved();
        };

        var trace = new CollectingTraceListener();
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            TestFixture.RunInStaThread(() =>
            {
                Trace.Listeners.Add(trace);
                try
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    scenario(dispatcher);
                    DrainDispatcher(dispatcher);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    DrainDispatcher(dispatcher);
                    // InvokeObservedAsync ? ContinueWith ?? TaskScheduler.Default??????
                    // ???????????????????
                    SpinWait.SpinUntil(() => trace.Messages.Count > 0, TimeSpan.FromSeconds(2));
                }
                finally
                {
                    Trace.Listeners.Remove(trace);
                }
            });
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }

        Assert.Null(unobserved);
        return trace;
    }

    private static void DrainDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class DispatchProbeViewModel : BaseViewModel
    {
        public void InitializeDispatcher()
        {
            Dispatch(() => { });
        }

        public void Post(Action action)
        {
            Dispatch(action);
        }
    }

    private sealed class CollectingTraceListener : TraceListener
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Enqueue(message);
            }
        }

        public override void WriteLine(string? message)
        {
            Write(message);
        }
    }
}



