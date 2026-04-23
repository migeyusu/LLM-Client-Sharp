using System.Text;
using System.Threading.Channels;
using LLMClient.Abstraction;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 一轮 ReAct 循环。实现 IAsyncEnumerable&lt;LoopEvent&gt; 以支持流式消费。
/// 生产者通过 internal 方法写入事件，消费者通过 await foreach 读取。
/// </summary>
public sealed class ReactStep : IAsyncEnumerable<LoopEvent>
{
    private readonly Channel<LoopEvent> _channel =
        Channel.CreateUnbounded<LoopEvent>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// 该轮结束后的结果（在内层枚举结束后可用）
    /// </summary>
    public StepResult? Result { get; private set; }

    public StringBuilder ProtocolLog { get; } = new();

    // ── 生产者 API（internal）──

    internal void Emit(LoopEvent evt) => _channel.Writer.TryWrite(evt);

    internal void EmitText(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Emit(new TextDelta(text));
    }

    internal void EmitReasoning(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Emit(new ReasoningDelta(text));
    }

    internal void EmitDiagnostic(DiagLevel level, string message)
        => Emit(new DiagnosticMessage(level, message));

    internal void EmitHistoryCompressionStarted(HistoryCompressionKind kind)
        => Emit(new HistoryCompressionStarted(kind));

    internal void EmitHistoryCompressionCompleted(HistoryCompressionKind kind, bool applied)
        => Emit(new HistoryCompressionCompleted(kind, applied));

    internal Task<bool> RequestPermissionAsync(object content)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Emit(new PermissionRequest(content, tcs));
        return tcs.Task;
    }

    internal void Complete(StepResult result)
    {
        Result = result;
        _channel.Writer.TryComplete();
    }

    internal void CompleteWithError(Exception ex, StepResult? partialResult = null)
    {
        Result = partialResult ?? new StepResult { Exception = ex, IsCompleted = false };
        _channel.Writer.TryComplete();
    }

    // ── 消费者 API ──

    public IAsyncEnumerator<LoopEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
}