using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace LLMClient.Rag;

public class SKDocChunk
{
    [VectorStoreKey] public Guid Key { get; set; } = Guid.Empty;

    /// <summary>
    /// raw data
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [TextSearchResultValue]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文档的唯一标识符
    /// </summary>
    [VectorStoreData]
    public string DocumentId { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// pdf:bookmark
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData] public int Level { get; set; }

    [VectorStoreData] public Guid ParentKey { get; set; } = Guid.Empty;

    [VectorStoreData] public bool HasChild { get; set; }

    [VectorStoreVector(1536)] public string TextEmbedding => Text;

    [VectorStoreVector(1536)] public string SummaryEmbedding => Summary;
}