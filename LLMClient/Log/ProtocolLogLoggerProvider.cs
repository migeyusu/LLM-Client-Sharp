using System.Text;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using Microsoft.Extensions.Logging;

namespace LLMClient.Log;

/// <summary>
/// 扩展方法：为 ILoggerFactory 添加将聊天请求日志写入 ChatContext.CurrentStep.ProtocolLog 的能力。
/// 当 ChatContext.ShowRequestJson 为 true 时，拦截 UseLogging 产生的日志并写入协议历史。
/// </summary>
public static class ProtocolLogLoggerExtensions
{
    /// <summary>
    /// 创建一个包装的 LoggerFactory，它能够将聊天请求日志写入 ChatContext.CurrentStep.ProtocolLog。
    /// </summary>
    public static ILoggerFactory CreateLoggerFactoryWithProtocolLog(this ILoggerFactory innerFactory)
    {
        return new ProtocolLogLoggerFactory(innerFactory);
    }
}

/// <summary>
/// 包装的 LoggerFactory，将日志同时输出到原始目标和 ChatContext.CurrentStep.ProtocolLog。
/// </summary>
public sealed class ProtocolLogLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory _innerFactory;

    public ProtocolLogLoggerFactory(ILoggerFactory innerFactory)
    {
        _innerFactory = innerFactory;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var innerLogger = _innerFactory.CreateLogger(categoryName);
        return new ProtocolLogLogger(innerLogger);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        _innerFactory.AddProvider(provider);
    }

    public void Dispose()
    {
        // 不释放 innerFactory，因为它由外部管理
    }

    private sealed class ProtocolLogLogger : ILogger
    {
        private readonly ILogger _innerLogger;

        public ProtocolLogLogger(ILogger innerLogger)
        {
            _innerLogger = innerLogger;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _innerLogger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _innerLogger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // 始终写入原始日志目标
            _innerLogger.Log(logLevel, eventId, state, exception, formatter);

            // 当 ShowRequestJson 为 true 时，捕获 UseLogging 的日志写入 ProtocolLog
            var context = AsyncContextStore<ChatContext>.Current;
            if (context?.ShowRequestJson != true)
            {
                return;
            }

            var history = context.CurrentStep?.ProtocolLog;
            if (history == null)
            {
                return;
            }

            // 过滤：仅捕获 Microsoft.Extensions.AI.LoggingChatClient 产生的请求/响应日志
            // EventId 1-4 对应 ChatRequest, StreamingChatRequest, ChatResponse, StreamingChatResponse
            if (!IsChatClientLog(eventId))
            {
                return;
            }

            var message = formatter(state, exception);

            lock (history)
            {
                // 请求开始
                if (eventId.Id == 1 || eventId.Id == 2)
                {
                    history.AppendLine("<request>");
                }

                // 写入消息内容
                history.AppendLine(message);

                // 请求完成
                if (eventId.Id == 3 || eventId.Id == 4)
                {
                    history.AppendLine("</request>");
                }
            }
        }

        /// <summary>
        /// 判断是否为 Microsoft.Extensions.AI.LoggingChatClient 产生的聊天日志。
        /// EventId 定义见：https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI/LoggingChatClient.cs
        /// - 1: ChatRequest
        /// - 2: StreamingChatRequest
        /// - 3: ChatResponse
        /// - 4: StreamingChatResponse
        /// </summary>
        private static bool IsChatClientLog(EventId eventId)
        {
            return eventId.Id >= 1 && eventId.Id <= 4;
        }
    }
}
