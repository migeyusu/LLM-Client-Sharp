// LogEntry.cs

using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    // 可选：可以添加 CategoryName 等其他字段
}