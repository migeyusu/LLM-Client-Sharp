namespace LLMClient.Abstraction;

public interface IChatEndpoint
{
    /// <summary>
    /// 唯一名称
    /// </summary>
    string Name { get; }

    bool IsResponding { get; set; }

    /// <summary>
    /// 以 ReAct 循环流的形式发送请求。每个 ReactStep 代表一轮 Reasoning + Acting。
    /// </summary>
    IAsyncEnumerable<ReactStep> SendRequestAsync(
        RequestContext context,
        CancellationToken cancellationToken = default);
}