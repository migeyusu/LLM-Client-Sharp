using LLMClient.Rag;

namespace LLMClient.Abstraction;

public interface IRagSource
{
    string Name { get; set; }

    Guid Id { get; }

    ConstructStatus Status { get; }

    /// <summary>
    /// 从向量数据库或其他存储中加载节点。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    Task LoadAsync();

    /// <summary>
    /// 构建节点的向量表示或其他必要的处理。
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ConstructAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询节点以获取相关信息或答案。
    /// </summary>
    /// <param name="query"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ISearchResult> QueryAsync(string query, dynamic options, CancellationToken cancellationToken = default);
}

public interface IRagSourceCollection : IReadOnlyCollection<IRagSource>
{
    Task LoadAsync();
}