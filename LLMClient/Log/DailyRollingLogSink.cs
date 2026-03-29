using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

/// <summary>
/// 负责将日志按日期滚动写入文件的共享底层写入器。
/// </summary>
public sealed class DailyRollingLogSink : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly string _logDirectory;
    private readonly string _filePrefix;
    private readonly Func<DateTimeOffset> _nowProvider;

    private StreamWriter? _writer;
    private DateOnly? _currentDate;
    private bool _disposed;

    public DailyRollingLogSink(string logDirectory, string filePrefix = "trace",
        Func<DateTimeOffset>? nowProvider = null)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            throw new ArgumentException("Log directory cannot be null or empty.", nameof(logDirectory));
        }

        if (string.IsNullOrWhiteSpace(filePrefix))
        {
            throw new ArgumentException("File prefix cannot be null or empty.", nameof(filePrefix));
        }

        _logDirectory = Path.GetFullPath(logDirectory);
        _filePrefix = filePrefix;
        _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

        Directory.CreateDirectory(_logDirectory);
    }

    public void WriteTrace(TraceEventType eventType, string? message, string? source = null, int? eventId = null,
        bool flush = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteCore(timestamp => FormatTraceEntry(timestamp, eventType, source, eventId, message), flush);
    }

    public void WriteLog(LogLevel logLevel, string? categoryName, EventId eventId, string? message,
        Exception? exception = null, bool flush = false)
    {
        if (logLevel == LogLevel.None)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        WriteCore(timestamp => FormatLoggerEntry(timestamp, logLevel, categoryName, eventId, message, exception), flush);
    }

    public void Flush()
    {
        lock (_syncRoot)
        {
            _writer?.Flush();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            _currentDate = null;
        }
    }

    private void WriteCore(Func<DateTimeOffset, string> formatter, bool flush)
    {
        try
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                var timestamp = _nowProvider();
                var writer = EnsureWriter(timestamp);
                writer.WriteLine(formatter(timestamp));
                if (flush)
                {
                    writer.Flush();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Debug.WriteLine($"Failed to persist log entry: {exception}");
        }
    }

    private StreamWriter EnsureWriter(DateTimeOffset timestamp)
    {
        var currentDate = DateOnly.FromDateTime(timestamp.Date);
        if (_writer is not null && _currentDate == currentDate)
        {
            return _writer;
        }

        _writer?.Flush();
        _writer?.Dispose();

        Directory.CreateDirectory(_logDirectory);
        var filePath = Path.Combine(_logDirectory, $"{_filePrefix}-{timestamp:yyyy-MM-dd}.log");
        var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _currentDate = currentDate;
        return _writer;
    }

    private static string FormatTraceEntry(DateTimeOffset timestamp, TraceEventType eventType, string? source, int? eventId,
        string message)
    {
        var builder = CreateEntryHeader(timestamp, GetLevelName(eventType));

        if (!string.IsNullOrWhiteSpace(source))
        {
            builder.Append(' ');
            builder.Append('[').Append(source).Append(']');
        }

        if (eventId is > 0)
        {
            builder.Append(' ');
            builder.Append("[EventId:").Append(eventId.Value).Append(']');
        }

        builder.Append(' ');
        builder.Append(message);
        return builder.ToString();
    }

    private static string FormatLoggerEntry(DateTimeOffset timestamp, LogLevel logLevel, string? categoryName,
        EventId eventId, string? message, Exception? exception)
    {
        var builder = CreateEntryHeader(timestamp, GetLevelName(logLevel));

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            builder.Append(' ');
            builder.Append('[').Append(categoryName).Append(']');
        }

        if (eventId.Id != 0)
        {
            builder.Append(' ');
            builder.Append("[EventId:").Append(eventId.Id).Append(']');
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append(' ');
            builder.Append(message);
        }

        if (exception is not null)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                builder.AppendLine();
            }
            else
            {
                builder.Append(' ');
            }

            builder.Append(exception);
        }

        return builder.ToString();
    }

    private static StringBuilder CreateEntryHeader(DateTimeOffset timestamp, string levelName)
    {
        var builder = new StringBuilder();
        builder.Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        builder.Append(' ');
        builder.Append('[').Append(levelName).Append(']');
        return builder;
    }

    private static string GetLevelName(TraceEventType eventType) => eventType switch
    {
        TraceEventType.Critical => "Critical",
        TraceEventType.Error => "Error",
        TraceEventType.Warning => "Warning",
        TraceEventType.Information => "Information",
        TraceEventType.Verbose => "Verbose",
        TraceEventType.Start => "Start",
        TraceEventType.Stop => "Stop",
        TraceEventType.Suspend => "Suspend",
        TraceEventType.Resume => "Resume",
        TraceEventType.Transfer => "Transfer",
        _ => eventType.ToString()
    };

    private static string GetLevelName(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => logLevel.ToString()
    };
}
