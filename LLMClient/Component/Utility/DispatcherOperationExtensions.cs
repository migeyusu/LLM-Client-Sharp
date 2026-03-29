using System.Diagnostics;
using System.Windows.Threading;

namespace LLMClient.Component.Utility;

internal static class DispatcherOperationExtensions
{
    public static DispatcherOperation InvokeObservedAsync(this Dispatcher dispatcher, Action action,
        DispatcherPriority priority = DispatcherPriority.Normal, string? operationName = null)
    {
        var operation = dispatcher.InvokeAsync(action, priority);
        operation.ObserveFault(operationName);
        return operation;
    }

    public static void ObserveFault(this DispatcherOperation operation, string? operationName = null)
    {
        _ = operation.Task.ContinueWith(static (task, state) =>
            {
                var exception = task.Exception;
                _ = exception;
                Trace.TraceError("{0} failed: {1}",
                    state as string ?? "Dispatcher operation",
                    exception?.Flatten());
            },
            operationName,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
