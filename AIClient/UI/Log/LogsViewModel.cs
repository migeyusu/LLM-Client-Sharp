// LogsViewModel.cs

using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using LLMClient.UI.Component;
using Microsoft.Extensions.Logging;

namespace LLMClient.UI.Log;

public class LogsViewModel : ILogger
{
    // 用于UI绑定的高性能集合
    public SuspendableObservableCollection<LogEntry> LogEntries { get; }

    // 线程安全的队列，用于从任何线程接收日志
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();

    // UI线程调度计时器，用于定期处理队列
    private readonly DispatcherTimer _timer;

    public ICommand ClearLogsCommand => new RelayCommand(() =>
    {
        LogEntries.Clear();
        _logQueue.Clear();
    });

    public LogsViewModel()
    {
        LogEntries = new SuspendableObservableCollection<LogEntry>();

        // DispatcherTimer的Tick事件总是在UI线程上触发，无需手动调度
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100) // 每100ms更新一次UI
        };
        _timer.Tick += ProcessLogQueue;
    }

    public void Start() => _timer.Start();

    public void Stop()
    {
        //立刻处理剩余的日志
        ProcessLogQueue(null, EventArgs.Empty);
        _timer.Stop();
    }

    /// <summary>
    /// 这是 ILogger 接口的核心实现。
    /// 任何线程都可以调用此方法来记录日志。
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // 创建LogEntry并放入线程安全的队列中，此操作非常快速
        _logQueue.Enqueue(new LogEntry
        {
            Timestamp = DateTime.Now,
            LogLevel = logLevel,
            Message = formatter(state, exception),
            Exception = exception
        });
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    // 对于UI日志记录器，范围通常不是必须的
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <summary>
    /// 此方法由DispatcherTimer在UI线程上定期调用。
    /// </summary>
    private void ProcessLogQueue(object? sender, EventArgs e)
    {
        if (_logQueue.IsEmpty)
        {
            return;
        }

        // 从队列中批量取出所有待处理的日志
        var logsToProcess = new List<LogEntry>();
        while (_logQueue.TryDequeue(out var logEntry))
        {
            logsToProcess.Add(logEntry);
        }

        if (logsToProcess.Count == 0) return;
        if (logsToProcess.Count > 1000)
        {
            LogEntries.AddRange(logsToProcess);
        }
        else
        {
            logsToProcess.ForEach(logEntry => LogEntries.Add(logEntry));
        }
    }
}