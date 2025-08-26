using System.Text.Json.Serialization;
using LLMClient.Rag;

namespace LLMClient.Abstraction;

[JsonDerivedType(typeof(PdfFile), "PdfFile")]
[JsonDerivedType(typeof(TextFile), "TextFile")]
[JsonDerivedType(typeof(WordFile), "WordFile")]
[JsonDerivedType(typeof(ExcelFile), "ExcelFile")]
[JsonDerivedType(typeof(MarkdownFile), "MarkdownFile")]
public interface IRagSource : IAIFunctionGroup
{
    string ResourceName { get; }

    Guid Id { get; }

    RagFileStatus Status { get; }

    /// <summary>
    /// 初始化，包含从向量数据库或其他存储中加载节点的过程。
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    Task InitializeAsync();

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

    Task<ISearchResult> GetStructureAsync(CancellationToken cancellationToken = default);
}