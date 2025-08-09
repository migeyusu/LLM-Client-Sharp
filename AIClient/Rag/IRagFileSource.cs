using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;

namespace LLMClient.Rag;

public enum DocumentFileType
{
    Text,
    Word,
    Pdf,
    Excel,
}

[JsonDerivedType(typeof(PdfFile), "PdfFile")]
[JsonDerivedType(typeof(TextFile), "TextFile")]
[JsonDerivedType(typeof(WordFile), "WordFile")]
[JsonDerivedType(typeof(ExcelFile), "ExcelFile")]
public interface IRagFileSource : IRagSource
{
    string FilePath { get; }

    DocumentFileType FileType { get; }

    DateTime EditTime { get; }

    long FileSize { get; }

    bool HasConstructed { get; set; }

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
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default);
}