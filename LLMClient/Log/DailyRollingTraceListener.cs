using System.Diagnostics;

namespace LLMClient.Log;

/// <summary>
/// 将 <see cref="Trace"/> 日志按日期持久化到文件。
/// </summary>
public sealed class DailyRollingTraceListener : TraceListener
{
    private readonly DailyRollingLogSink _sink;
    private readonly bool _ownsSink;
    private bool _disposed;

    public DailyRollingTraceListener(string logDirectory, string filePrefix = "trace",
        Func<DateTimeOffset>? nowProvider = null)
        : this(new DailyRollingLogSink(logDirectory, filePrefix, nowProvider), ownsSink: true)
    {
    }

    public DailyRollingTraceListener(DailyRollingLogSink sink, bool ownsSink = false)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _ownsSink = ownsSink;
    }

    public override void Write(string? message)
    {
        _sink.WriteTrace(TraceEventType.Information, message, flush: Trace.AutoFlush);
    }

    public override void WriteLine(string? message)
    {
        _sink.WriteTrace(TraceEventType.Information, message, flush: Trace.AutoFlush);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id,
        string? message)
    {
        _sink.WriteTrace(eventType, message, source, id, flush: Trace.AutoFlush);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id,
        string? format, params object?[]? args)
    {
        if (string.IsNullOrEmpty(format))
        {
            return;
        }

        var message = args is { Length: > 0 } ? string.Format(format, args) : format;
        _sink.WriteTrace(eventType, message, source, id, flush: Trace.AutoFlush);
    }

    public override void TraceData(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id,
        object? data)
    {
        _sink.WriteTrace(eventType, data?.ToString(), source, id, flush: Trace.AutoFlush);
    }

    public override void TraceData(TraceEventCache? eventCache, string? source, TraceEventType eventType, int id,
        params object?[]? data)
    {
        var message = data is { Length: > 0 }
            ? string.Join(", ", data.Select(item => item?.ToString()))
            : null;
        _sink.WriteTrace(eventType, message, source, id, flush: Trace.AutoFlush);
    }

    public override void Fail(string? message, string? detailMessage)
    {
        var combinedMessage = string.IsNullOrWhiteSpace(detailMessage)
            ? message
            : $"{message}{Environment.NewLine}{detailMessage}";
        _sink.WriteTrace(TraceEventType.Critical, combinedMessage, nameof(Trace), flush: Trace.AutoFlush);
    }

    public override void Flush()
    {
        _sink.Flush();
    }

    public override void Close()
    {
        Dispose(true);
        base.Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(false);
            return;
        }

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsSink)
        {
            _sink.Dispose();
        }

        base.Dispose(true);
    }
}
