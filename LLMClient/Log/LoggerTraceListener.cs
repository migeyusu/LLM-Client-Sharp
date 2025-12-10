using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

/// <summary>
/// 将 System.Diagnostics.Trace 输出重定向到 Microsoft.Extensions.Logging 的 TraceListener
/// 支持通过调用堆栈自动确定日志分类器，兼容标准的 ILogger 分类特性
/// </summary>
public class LoggerTraceListener : TraceListener
{
    private readonly ILogger _logger;

    public LoggerTraceListener(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<LoggerTraceListener>();
    }

    public override void Write(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _logger.LogInformation(message);
        }
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _logger.LogInformation(message);
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source,
        TraceEventType eventType, int id, string? message)
    {
        if (string.IsNullOrEmpty(message)) return;

        var logLevel = eventType switch
        {
            TraceEventType.Critical => LogLevel.Critical,
            TraceEventType.Error => LogLevel.Error,
            TraceEventType.Warning => LogLevel.Warning,
            TraceEventType.Information => LogLevel.Information,
            TraceEventType.Verbose => LogLevel.Debug,
            TraceEventType.Start or TraceEventType.Stop or TraceEventType.Suspend or TraceEventType.Resume
                or TraceEventType.Transfer => LogLevel.Trace,
            _ => LogLevel.Information
        };

        // 如果有 source 信息，将其作为结构化日志的一部分
        if (!string.IsNullOrEmpty(source))
        {
            _logger.Log(logLevel, id, "[{Source}] {Message}", source, message);
        }
        else
        {
            _logger.Log(logLevel, id, message);
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id,
        string? format, params object?[]? args)
    {
        if (string.IsNullOrEmpty(format)) return;
        var message = args?.Length > 0 ? string.Format(format, args) : format;
        TraceEvent(eventCache, source, eventType, id, message);
    }
}