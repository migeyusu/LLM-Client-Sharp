using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using OpenAI;

namespace LLMClient.Rag;

public class SemanticKernelStore
{
    public enum SearchAlgorithm
    {
        Default,
        TopDown,
        Graph, // Graph search algorithm is not implemented yet
        Indirect,
    }
    
    private readonly string _dbConnectionString;

    private Kernel? _kernel;

    public SemanticKernelStore(string dbConnectionString = "Data Source=mydatabase.db")
    {
        _dbConnectionString = dbConnectionString;
    }

    [Experimental("SKEXP0001")]
    public void InitializeKernel(OpenAIClient client, string modelId = "text-embedding-v3")
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddOpenAIEmbeddingGenerator("text-embedding-v3", client)
            // 添加 SQLite 向量存储（连接字符串指向本地文件）
            .AddSqliteVectorStore(connectionStringProvider: sp => _dbConnectionString); // 自动创建 db 文件
        _kernel = kernelBuilder.Build();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="docId"></param>
    /// <param name="chunks"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task AddFile(string docId, IEnumerable<SKDocChunk> chunks, CancellationToken token)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("docId cannot be null or empty", nameof(docId));
        }

        if (_kernel == null)
        {
            throw new InvalidOperationException("Kernel is not initialized. Call InitializeKernel first.");
        }

        var vectorStore = _kernel.GetRequiredService<VectorStore>();
        var docCollection = vectorStore.GetCollection<string, SKDocChunk>(docId);
        await docCollection.EnsureCollectionExistsAsync(token);
        await docCollection.UpsertAsync(chunks, token);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="docId"></param>
    /// <param name="token"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task RemoveFile(string docId, CancellationToken token)
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Kernel is not initialized. Call InitializeKernel first.");
        }

        var vectorStore = _kernel.GetRequiredService<VectorStore>();
        var docCollection = vectorStore.GetCollection<string, SKDocChunk>(docId);
        await docCollection.EnsureCollectionDeletedAsync(token);
    }

    public async Task<IEnumerable<string>> SearchAsync(string query, string docId,
        SearchAlgorithm algorithm, int topK = 5, CancellationToken token = default)
    {
        switch (algorithm)
        {
            case SearchAlgorithm.Default:
                return await GeneralSearchAsync(query, docId, token, topK);
            case SearchAlgorithm.TopDown:
                return await TopDownSearchAsync(query, docId, token, topK);
            case SearchAlgorithm.Indirect:
                return await IndirectSearchAsync(query, docId, token, 2, topK);
                break;
            case SearchAlgorithm.Graph:
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null);
        }
    }


    public async Task<IEnumerable<string>> TopDownSearchAsync(string query, string docId,
        CancellationToken token, int topK = 5)
    {
        var topLevelResults = await InternalSearchAsync(query, new VectorSearchOptions<SKDocChunk>()
        {
            Filter = chunk => chunk.Level == 0,
            VectorProperty = chunk => chunk.SummaryEmbedding
        }, docId, token);
        if (topLevelResults.Count == 0)
        {
            return [];
        }

        var results = await HierarchicalSearchAsync(topLevelResults, docId, query, token)
            .ToArrayAsync(cancellationToken: token);
        return results.OrderByDescending(result => result.Score).Take(topK).Select(result => result.Record.Text);
    }

    private async IAsyncEnumerable<VectorSearchResult<SKDocChunk>> HierarchicalSearchAsync(
        IEnumerable<VectorSearchResult<SKDocChunk>> chunks, string docId,
        string query, [EnumeratorCancellation] CancellationToken token)
    {
        foreach (var chunk in chunks)
        {
            var record = chunk.Record;
            if (record.HasChild)
            {
                var key = record.Key;
                var childResults = await InternalSearchAsync(query, new VectorSearchOptions<SKDocChunk>()
                {
                    Filter = docChunk => docChunk.ParentKey == key,
                    VectorProperty = docChunk => docChunk.SummaryEmbedding
                }, docId, token, 10);
                if (childResults.Count > 0)
                {
                    //如果有子节点，继续查找子节点
                    await foreach (var childChunk in HierarchicalSearchAsync(childResults, docId, query, token))
                    {
                        yield return childChunk;
                    }
                }
            }
            else
            {
                yield return chunk;
            }
        }
    }

    public async Task<IEnumerable<string>> IndirectSearchAsync(string query, string docId, CancellationToken token,
        int level = 2, int topK = 5)
    {
        var vectorSearchOptions = new VectorSearchOptions<SKDocChunk>()
        {
            //只搜索没有子节点的文档
            Filter = chunk => !chunk.HasChild,
            VectorProperty = chunk => chunk.TextEmbedding
        };
        var results = new List<string>();
        const int topKChild = 5;
        var preResults = (await InternalSearchAsync(query, vectorSearchOptions, docId, token, topKChild)).ToList();
        while (level-- > 0 && preResults.Count > 0)
        {
            var searchResults = preResults.ToArray();
            preResults.Clear();
            foreach (var chunk in searchResults)
            {
                var record = chunk.Record;
                var recordText = record.Text;
                results.Add(recordText);
                preResults.AddRange(await InternalSearchAsync(record.Summary, vectorSearchOptions, docId, token, topK));
            }
        }

        return results.Take(topK);
    }

    public async Task<IEnumerable<string>> GeneralSearchAsync(string query, string docId, CancellationToken token,
        int topK = 5)
    {
        var vectorSearchOptions = new VectorSearchOptions<SKDocChunk>()
        {
            //只搜索没有子节点的文档
            Filter = chunk => !chunk.HasChild,
            VectorProperty = chunk => chunk.TextEmbedding
        };
        return (await InternalSearchAsync(query, vectorSearchOptions, docId, token, topK))
            .Select(chunk => chunk.Record.Text);
    }

    private async Task<IList<VectorSearchResult<T>>> InternalSearchAsync<T>(string query,
        VectorSearchOptions<T> searchOptions, string collectionId,
        CancellationToken token, int topK = 5) where T : class
    {
        if (_kernel == null)
        {
            throw new InvalidOperationException("Kernel is not initialized. Call InitializeKernel first.");
        }

        var vectorStore = _kernel.GetRequiredService<VectorStore>();
        var docCollection = vectorStore.GetCollection<string, T>(collectionId);
        await docCollection.EnsureCollectionExistsAsync(token);
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embedding = await embeddingGenerator.GenerateAsync(query, cancellationToken: token);
        return await docCollection.SearchAsync(
            embedding.Vector,
            options: searchOptions,
            top: topK,
            cancellationToken: token).ToArrayAsync(cancellationToken: token);
    }
}