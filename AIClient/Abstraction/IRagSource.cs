using System.Text.Json.Serialization;
using LLMClient.Rag;

namespace LLMClient.Abstraction;

/// <summary>
/// rag source 不但要求实现搜索接口，还要求实现AI功能组接口。
/// 这样可以确保RAG源不仅能够提供相关信息，还能执行与AI相关的任务。
/// 例如，可以通过RAG源来执行文本生成、摘要、翻译等AI功能。
/// 这种设计使得RAG源在系统中具有更高的灵活性和功能性，能够更好地满足复杂的应用需求。
/// <para>尽管理论上可以通过适配器模式（Adaptor）包装RagSource实现更简化抽象，
/// 但会增加复杂性，比如不同搜索服务的搜索参数会不同；更重要的是连方法都会不同</para>
/// </summary>
[JsonDerivedType(typeof(PdfFile), "PdfFile")]
[JsonDerivedType(typeof(TextFile), "TextFile")]
[JsonDerivedType(typeof(WordFile), "WordFile")]
[JsonDerivedType(typeof(ExcelFile), "ExcelFile")]
[JsonDerivedType(typeof(MarkdownFile), "MarkdownFile")]
public interface IRagSource : ISearchService, IAIFunctionGroup
{
    string ResourceName { get; }

    Guid Id { get; }

    RagStatus Status { get; }

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
}