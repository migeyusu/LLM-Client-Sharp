using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// 单轮 ReAct 循环内的事件（Discriminated Union 风格）
/// </summary>
public abstract record LoopEvent;

public enum HistoryCompressionKind
{
    PreambleSummary,
    ObservationMasking,
    InfoCleaning,
    TaskSummary,
}

// ── 流式内容 ──

/// <summary>
/// LLM 输出的文本增量
/// </summary>
public sealed record TextDelta(string Text) : LoopEvent;

/// <summary>
/// LLM 输出的推理/思考增量
/// </summary>
public sealed record ReasoningDelta(string Text) : LoopEvent;

// ── 工具调用 ──

/// <summary>
/// 工具调用开始
/// </summary>
public sealed record FunctionCallStarted(FunctionCallContent Call) : LoopEvent;

/// <summary>
/// 工具调用完成
/// </summary>
public sealed record FunctionCallCompleted(
    string CallId,
    string FunctionName,
    object? Result,
    Exception? Error) : LoopEvent;

// ── 历史压缩状态 ──

/// <summary>
/// 历史压缩开始。
/// </summary>
public sealed record HistoryCompressionStarted(HistoryCompressionKind Kind) : LoopEvent;

/// <summary>
/// 历史压缩结束。Applied=false 表示无需压缩或本次未发生实际替换。
/// </summary>
public sealed record HistoryCompressionCompleted(
    HistoryCompressionKind Kind,
    bool Applied) : LoopEvent;

// ── 日志/诊断 ──

public enum DiagLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// 诊断消息
/// </summary>
public sealed record DiagnosticMessage(DiagLevel Level, string Message) : LoopEvent;

// ── 交互式权限请求 ──

/// <summary>
/// 权限请求事件。生产者写入后 await Response.Task，消费者读取后调用 Response.SetResult。
/// </summary>
public sealed record PermissionRequest(
    object Content,
    TaskCompletionSource<bool> Response) : LoopEvent;

