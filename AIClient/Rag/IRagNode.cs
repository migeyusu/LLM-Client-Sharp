using System.Text.Json.Serialization;
using System.Windows.Shell;

namespace LLMClient.Rag;

[JsonDerivedType(typeof(PdfFile))]
public interface IRagSource
{
}

/// <summary>
/// 基于web搜索的节点。
/// </summary>
public interface IRagWebSource : IRagSource
{
}

public interface IRagFileSource : IRagSource
{
    string FilePath { get; }

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

    /// <summary>
    /// 查询节点以获取相关信息或答案。
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default);
}

public interface ISearchResult
{
}