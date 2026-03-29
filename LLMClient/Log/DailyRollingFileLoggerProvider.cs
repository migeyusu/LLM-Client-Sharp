using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

/// <summary>
/// 将 <see cref="ILogger"/> 输出持久化到按日期滚动的日志文件中。
/// </summary>
public sealed class DailyRollingFileLoggerProvider : ILoggerProvider
{
    private readonly DailyRollingLogSink _sink;
    private readonly bool _ownsSink;

    public DailyRollingFileLoggerProvider(string logDirectory, string filePrefix = "trace",
        Func<DateTimeOffset>? nowProvider = null)
        : this(new DailyRollingLogSink(logDirectory, filePrefix, nowProvider), ownsSink: true)
    {
    }

    public DailyRollingFileLoggerProvider(DailyRollingLogSink sink, bool ownsSink = false)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _ownsSink = ownsSink;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DailyRollingFileLogger(categoryName, _sink);
    }

    public void Dispose()
    {
        if (_ownsSink)
        {
            _sink.Dispose();
        }
    }

    private sealed class DailyRollingFileLogger(string categoryName, DailyRollingLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            sink.WriteLog(logLevel, categoryName, eventId, message, exception, flush: Trace.AutoFlush);
        }
    }
}
