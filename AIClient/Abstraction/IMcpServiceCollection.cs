namespace LLMClient.Abstraction;

public interface IMcpServiceCollection : IEnumerable<IAIFunctionGroup>
{
    bool IsInitialized { get; }

    bool IsLoaded { get; }

    Task InitializeToolsAsync();

    Task LoadAsync();

    Task EnsureAsync();

    /// <summary>
    /// 这个方法的存在是因为mcp服务可能需要共享单例或者是多例的实例。
    /// 如果是单例，那么会返回符合传入的<see cref="functionGroup"/>相同参数的实例。
    /// 如果是多例，那么会返回一个新的实例。
    /// </summary>
    /// <param name="functionGroup"></param>
    /// <returns></returns>
    IAIFunctionGroup TryGet(IAIFunctionGroup functionGroup);
}
