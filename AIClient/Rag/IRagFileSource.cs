using LLMClient.Abstraction;

namespace LLMClient.Rag;

public enum DocumentFileType
{
    Text,
    Word,
    Pdf,
    Excel,
    Markdown,
}

public interface IRagFileSource : IRagSource
{
    string FilePath { get; }

    DocumentFileType FileType { get; }

    DateTime EditTime { get; }

    long FileSize { get; }

    string DocumentId { get; }

    /// <summary>
    /// 将文档结构输出
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ISearchResult> GetStructureAsync(CancellationToken cancellationToken = default);
}

public enum RagStatus
{
    NotConstructed,
    Constructing,
    Constructed,
    Error
}