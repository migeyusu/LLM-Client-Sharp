// File: LLMClient/ContextEngineering/Tools/IEmbeddingService.cs

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Embedding 服务抽象，供 CodeSearchService 调用进行语义检索。
/// 具体实现由 RAG 模块提供。
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// 执行语义检索，返回最相关的代码片段
    /// </summary>
    /// <param name="query">用户查询文本</param>
    /// <param name="topK">返回结果数量上限</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>代码片段及相似度分数</returns>
    Task<List<(string filePath, int startLine, int endLine, string snippet, double score)>> 
        SearchByEmbeddingAsync(string query, int topK, CancellationToken ct = default);
}