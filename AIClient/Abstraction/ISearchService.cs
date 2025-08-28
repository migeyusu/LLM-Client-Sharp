namespace LLMClient.Abstraction;

/// <summary>
/// 用于实现搜索服务。可以是基于向量数据库的搜索，也可以是网页搜索等。
/// <para>搜索服务一部分会封装为AIFunction；另一部分会被内部服务（SearchAgent）调用</para>
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// 查询节点以获取相关信息或答案。
    /// </summary>
    /// <param name="query"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ISearchResult> QueryAsync(string query, dynamic options, CancellationToken cancellationToken = default);
}