using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

public sealed class CrashGuard : IDisposable
{
    private readonly string _logPath;
    private readonly Func<ILogger?> _loggerAccessor;
    private Application? _application;
    private int _handlingCrash;
    private bool _processHandlersRegistered;

    public CrashGuard(string logPath, Func<ILogger?> loggerAccessor)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
        _loggerAccessor = loggerAccessor ?? throw new ArgumentNullException(nameof(loggerAccessor));
    }

    public void RegisterProcessHandlers()
    {
        if (_processHandlersRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _processHandlersRegistered = true;
    }

    public void AttachApplication(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null)
        {
            _application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        }

        _application = application;
        _application.DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleCrash("DispatcherUnhandledException", e.Exception, isTerminating: false);
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
                        ?? new Exception($"Unhandled exception object type: {e.ExceptionObject?.GetType().FullName ?? "unknown"}");
        HandleCrash("AppDomain.UnhandledException", exception, isTerminating: e.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleCrash("TaskScheduler.UnobservedTaskException", e.Exception.Flatten(), isTerminating: false);
        e.SetObserved();
    }

    private void HandleCrash(string source, Exception exception, bool isTerminating)
    {
        // Avoid re-entrancy in crash paths to reduce the chance of recursive failure.
        if (Interlocked.Exchange(ref _handlingCrash, 1) == 1)
        {
            return;
        }

        try
        {
            var logger = _loggerAccessor();
            logger?.LogCritical(exception, "{Source} captured an unhandled exception", source);
            Trace.TraceError("{0} captured an unhandled exception: {1}", source, exception);

            if (CrashDumpWriter.TryWriteCurrentProcessDump(_logPath, out var dumpPath, out var dumpError))
            {
                logger?.LogCritical("Crash dump created at: {DumpPath}", dumpPath);
                Trace.TraceError("Crash dump created at: {0}", dumpPath);
                return;
            }

            logger?.LogError(dumpError, "Failed to create crash dump from {Source}", source);
            Trace.TraceError("Failed to create crash dump from {0}: {1}", source, dumpError);
        }
        catch (Exception loggingFailure)
        {
            try
            {
                Trace.TraceError("CrashGuard failed while handling crash: {0}", loggingFailure);
            }
            catch
            {
                // Last chance path: keep the process termination flow unblocked.
            }
        }
        finally
        {
            if (!isTerminating)
            {
                Interlocked.Exchange(ref _handlingCrash, 0);
            }
        }
    }

    public void Dispose()
    {
        if (_application is not null)
        {
            _application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            _application = null;
        }

        if (_processHandlersRegistered)
        {
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            _processHandlersRegistered = false;
        }
    }
}

