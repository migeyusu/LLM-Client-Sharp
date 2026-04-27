using LLMClient.Dialog.Models;

namespace LLMClient.Abstraction;

public interface IReactClient
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    bool IsResponding { get; set; }

    /// <summary>
    /// 以 ReAct 循环流的形式发送请求。每个 ReactStep 代表一轮 Reasoning + Acting。
    /// </summary>
    /// <param name="context">请求上下文</param>
    /// <param name="exit">
    /// 退出回调，在每个 ReactStep 完成后求值。返回 <c>true</c> 时终止 ReAct 循环。
    /// 若为 <c>null</c>，使用默认退出条件：<c>result.IsCompleted || result.Exception != null</c>。
    /// </param>
    /// <param name="cancellationToken">取消令牌</param>
    IAsyncEnumerable<ReactStep> SendRequestAsync(
        IRequestContext context,
        Predicate<ReactStep>? exit = null,
        CancellationToken cancellationToken = default);
}
