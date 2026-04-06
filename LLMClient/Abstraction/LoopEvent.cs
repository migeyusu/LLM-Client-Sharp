using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

/// <summary>
/// 单轮 ReAct 循环内的事件（Discriminated Union 风格）
/// </summary>
public abstract record LoopEvent;

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

