using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;

namespace LLMClient.Rag;

public enum SearchAlgorithm
{
    /// <summary>
    /// search flat list, only search graph nodes
    /// </summary>
    [Description("Default search algorithm as base approach in RAG, it searches all content chunks of document.")]
    Default,

    /// <summary>
    /// top-down hierarchical search, first search top-level nodes, then search child nodes
    /// </summary>
    [Description(
        "Top-down search algorithm, first search summary of top-level nodes(such as bookmarks), then search child nodes recursively.")]
    TopDown,

    // [Description("Not implemented yet, will search graph nodes in a graph structure.")]
    //Graph, // Graph search algorithm is not implemented yet

    [Description("Based on default search, use search result as query to search child nodes recursively.")]
    Recursive,

    //Interactive
}

public class SemanticKernelStore
{
    public const int ChunkDimension = 1536;

    private readonly Kernel _kernel;

    private OpenAIClientEx? CreateOpenAIClient(APIDefaultOption option)
    {
        if (string.IsNullOrEmpty(option.APIToken) || string.IsNullOrEmpty(option.URL))
        {
            return null;
        }

        var httpClient = new HttpClient( /*new DebugMessageLogger()*/) { Timeout = TimeSpan.FromMinutes(10) };
        return new OpenAIClientEx(new ApiKeyCredential(option.APIToken), new OpenAIClientOptions
        {
            Endpoint = new Uri(option.URL),
            Transport = new HttpClientPipelineTransport(httpClient)
        });
    }

    [Experimental("SKEXP0010")]
    public SemanticKernelStore(ILLMEndpoint endpoint, string modelId = "text-embedding-3-small",
        string dbConnectionString = "Data Source=file_embedding.db")
    {
        OpenAIClient? openAiClient;
        if (endpoint is APIEndPoint apiEndPoint)
        {
            openAiClient = CreateOpenAIClient(apiEndPoint.Option.ConfigOption);
        }
        else
        {
            throw new NotSupportedException(
                "Only APIEndPoint is supported for SemanticKernelStore. Please use OpenAI API endpoint.");
        }

        if (openAiClient == null)
        {
            throw new ArgumentException("OpenAIClient cannot be null. Ensure the endpoint is properly configured.",
                nameof(endpoint));
        }

        var s = dbConnectionString;
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddOpenAIEmbeddingGenerator(modelId, openAiClient)
            // 添加 SQLite 向量存储（连接字符串指向本地文件）
            .AddSqliteVectorStore(connectionStringProvider: _ => s, provider =>
                new SqliteVectorStoreOptions()
                {
                    EmbeddingGenerator =
                        new ProxyGenerator(provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>())
                }); // 自动创建 db 文件
        _kernel = kernelBuilder.Build();
    }

    class ProxyGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

        private readonly Embedding<float> _emptyEmbedding = new Embedding<float>(new float[ChunkDimension]);

        public ProxyGenerator(IEmbeddingGenerator<string, Embedding<float>> generator)
        {
            this._generator = generator;
        }


        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            //auto filter empty values
            var valueList = values.ToList();
            var embeddings = new Embedding<float>[valueList.Count];
            var nonEmptyValues = new List<(int Index, string Value)>();

            for (var i = 0; i < valueList.Count; i++)
            {
                if (string.IsNullOrEmpty(valueList[i]))
                {
                    embeddings[i] = _emptyEmbedding;
                }
                else
                {
                    nonEmptyValues.Add((i, valueList[i]));
                }
            }

            if (nonEmptyValues.Count > 0)
            {
                var generatedEmbeddings =
                    await _generator.GenerateAsync(nonEmptyValues.Select(v => v.Value), options, cancellationToken);
                var generatedList = generatedEmbeddings.ToList();
                for (int i = 0; i < nonEmptyValues.Count; i++)
                {
                    embeddings[nonEmptyValues[i].Index] = generatedList[i];
                }
            }

            return new GeneratedEmbeddings<Embedding<float>>(embeddings);
        }

        public void Dispose()
        {
            _generator.Dispose();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return _generator.GetService(serviceType, serviceKey);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="docId"></param>
    /// <param name="chunks"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task AddFile(string docId, IEnumerable<DocChunk> chunks, CancellationToken token)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("docId cannot be null or empty", nameof(docId));
        }

        var docCollection = GetDocCollection(docId);
        await docCollection.EnsureCollectionExistsAsync(token);
        await docCollection.UpsertAsync(chunks, token);
        /*foreach (var docChunk in chunks)
        {
            try
            {
                await docCollection.UpsertAsync(docChunk, token);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to upsert document {docId}, chunk key: {docChunk.Key}", e);
            }
        }*/
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="docId"></param>
    /// <param name="token"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task RemoveFileAsync(string docId, CancellationToken token)
    {
        var vectorStore = GetVectorStore();
        if (await vectorStore.CollectionExistsAsync(docId, token))
        {
            var docCollection = vectorStore.GetCollection<string, DocChunk>(docId);
            await docCollection.EnsureCollectionDeletedAsync(token);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="docId"></param>
    /// <param name="token"></param>
    /// <returns>roots of bookmarks</returns>
    public async Task<IList<ChunkNode>> GetStructureAsync(string docId, CancellationToken token)
    {
        VectorStoreCollection<string, DocChunk> vectorStoreCollection = GetDocCollection(docId);
        if (!await vectorStoreCollection.CollectionExistsAsync(token))
        {
            return Array.Empty<ChunkNode>();
        }

        var roots = await vectorStoreCollection
            .GetAsync(chunk => chunk.Level == 0, int.MaxValue / 2, cancellationToken: token)
            .Select(chunk => new ChunkNode(chunk))
            .ToArrayAsync(cancellationToken: token);
        foreach (var docChunk in roots)
        {
            await PopulateChildNodeAsync(vectorStoreCollection, docChunk, token);
        }

        return roots.OrderNode().ToArray();
    }

    /// <summary>
    /// 获取子节点（不包括Paragraph）
    /// </summary>
    /// <returns></returns>
    private async Task PopulateChildNodeAsync(VectorStoreCollection<string, DocChunk> collection, ChunkNode parentNode,
        CancellationToken token = default)
    {
        if (!parentNode.Chunk.HasChildNode)
        {
            //如果没有子节点，则不需要继续查找
            return;
        }

        var parentKey = parentNode.Chunk.Key;
        await foreach (var docChunk in collection.GetAsync(chunk => chunk.ParentKey == parentKey, int.MaxValue / 2,
                           cancellationToken: token))
        {
            if (docChunk.Type != (int)ChunkType.ContentUnit)
            {
                //只添加非段落类型的节点
                var childNode = new ChunkNode(docChunk);
                parentNode.AddChild(childNode);
                await PopulateChildNodeAsync(collection, childNode, token);
            }
        }
    }

    public async Task<IList<ChunkNode>> GetDocTreeAsync(string docId, CancellationToken token)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("docId cannot be null or empty", nameof(docId));
        }

        var collection = GetDocCollection(docId);
        if (!await collection.CollectionExistsAsync(token))
        {
            throw new InvalidOperationException($"Collection {docId} does not exist.");
        }

        var roots = await GetStructureAsync(docId, token);
        if (roots.Count == 0)
        {
            throw new InvalidOperationException($"No roots found in collection {docId}.");
        }

        foreach (var root in roots)
        {
            await PopulateChildParagraphAsync(collection, root, token);
        }

        return roots.OrderNode().ToArray();
    }

    public async Task<ChunkNode?> GetSectionAsync(string docId, string title, CancellationToken token)
    {
        if (string.IsNullOrEmpty(docId))
        {
            throw new ArgumentException("docId cannot be null or empty", nameof(docId));
        }

        var collection = GetDocCollection(docId);
        if (!await collection.CollectionExistsAsync(token))
        {
            throw new InvalidOperationException($"Collection {docId} does not exist.");
        }

        var sectionChunk = await collection.GetAsync(
                chunk => chunk.Type != (int)ChunkType.ContentUnit,
                int.MaxValue / 2, cancellationToken: token)
            .FirstOrDefaultAsync(chunk => chunk.Title.Contains(title),
                cancellationToken: token);
        if (sectionChunk == null)
        {
            return null;
        }

        //找到章节节点，继续填充子节点和段落
        var sectionNode = new ChunkNode(sectionChunk);
        await PopulateChildNodeAsync(collection, sectionNode, token);
        await PopulateChildParagraphAsync(collection, sectionNode, token);
        return sectionNode;
    }

    /// <summary>
    /// 填充所有子节点的段落
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="parentNode">必须是已经填充好结构的node</param>
    /// <param name="token"></param>
    private async Task PopulateChildParagraphAsync(VectorStoreCollection<string, DocChunk> collection,
        ChunkNode parentNode,
        CancellationToken token = default)
    {
        //递归到所有子节点
        if (!parentNode.Chunk.HasChildNode)
        {
            //表示当前为叶子节点
            var parentKey = parentNode.Chunk.Key;
            await foreach (var paragraphChunk in collection.GetAsync(
                               chunk => chunk.ParentKey == parentKey && chunk.Type == (int)ChunkType.ContentUnit,
                               int.MaxValue / 2, cancellationToken: token))
            {
                parentNode.AddChild(new ChunkNode(paragraphChunk));
            }

            return;
        }

        foreach (var nodeChild in parentNode.Children)
        {
            await PopulateChildParagraphAsync(collection, nodeChild, token);
        }
    }


    /// <summary>
    /// search with specific algorithm
    /// </summary>
    /// <param name="query"></param>
    /// <param name="docId"></param>
    /// <param name="algorithm"></param>
    /// <param name="topK"></param>
    /// <param name="token"></param>
    /// <returns>tree of result</returns>
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<IList<ChunkNode>> SearchAsync(string query, string docId,
        SearchAlgorithm algorithm, int topK = 6, CancellationToken token = default)
    {
        IEnumerable<DocChunk>? chunks;
        switch (algorithm)
        {
            case SearchAlgorithm.Default:
                chunks = await GeneralSearchAsync(query, docId, token, topK);
                break;
            case SearchAlgorithm.TopDown:
                chunks = await TopDownSearchAsync(query, docId, token, topK);
                break;
            case SearchAlgorithm.Recursive:
                chunks = await RecursiveSearchAsync(query, docId, token, 2, topK);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null);
        }

        var collection = GetDocCollection(docId);
        var baseNodes = chunks.Select((chunk => new ChunkNode(chunk))).ToArray();
        foreach (var chunkNode in baseNodes)
        {
            await PopulateParagraphs(collection, chunkNode);
        }

        IList<ChunkNode> rootNodes = new List<ChunkNode>();
        IDictionary<string, ChunkNode> cache = new Dictionary<string, ChunkNode>();
        //然后获取父节点
        foreach (var chunkNode in baseNodes)
        {
            await AddToRootNodes(rootNodes, cache, collection, chunkNode);
        }

        return rootNodes.OrderNode().ToArray();
    }

    /// <summary>
    /// Top-down search algorithm. Get the top-level nodes first, then recursively search child nodes.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="docId"></param>
    /// <param name="token"></param>
    /// <param name="topK"></param>
    /// <returns></returns>
    private async Task<IEnumerable<DocChunk>> TopDownSearchAsync(string query, string docId,
        CancellationToken token, int topK = 6)
    {
        var collection = _kernel.GetRequiredService<VectorStore>().GetCollection<string, DocChunk>(docId);
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var topLevelResults = await InternalSearchAsync(query, new VectorSearchOptions<DocChunk>()
        {
            Filter = chunk => chunk.Level == 0,
            VectorProperty = chunk => chunk.SummaryEmbedding
        }, docId, token, topK, embeddingGenerator, collection);
        if (topLevelResults.Count == 0)
        {
            return [];
        }

        //获取基础Bookmarks
        return (await HierarchicalSearchAsync(topLevelResults, docId, query, 10, token)
                .ToArrayAsync(cancellationToken: token))
            .OrderByDescending(result => result.Score).Take(topK)
            .Select(result => result.Record).Take(topK);
    }

    private async IAsyncEnumerable<VectorSearchResult<DocChunk>> HierarchicalSearchAsync(
        IEnumerable<VectorSearchResult<DocChunk>> chunks, string docId,
        string query, int topK, [EnumeratorCancellation] CancellationToken token,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        VectorStoreCollection<string, DocChunk>? collection = null)
    {
        foreach (var chunk in chunks)
        {
            var record = chunk.Record;
            if (record.HasChildNode)
            {
                var key = record.Key;
                var childResults = await InternalSearchAsync(query, new VectorSearchOptions<DocChunk>()
                {
                    Filter = docChunk => docChunk.ParentKey == key,
                    VectorProperty = docChunk => docChunk.SummaryEmbedding
                }, docId, token, topK, embeddingGenerator, collection);
                if (childResults.Count > 0)
                {
                    //如果有子节点，继续查找子节点
                    await foreach (var childChunk in HierarchicalSearchAsync(childResults, docId, query, topK, token,
                                       embeddingGenerator, collection))
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

    private async Task<IEnumerable<DocChunk>> RecursiveSearchAsync(string query, string docId,
        CancellationToken token,
        int level = 2, int topK = 6)
    {
        var collection = _kernel.GetRequiredService<VectorStore>().GetCollection<string, DocChunk>(docId);
        var embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var vectorSearchOptions = new VectorSearchOptions<DocChunk>()
        {
            //只搜索没有子节点的文档
            Filter = chunk => chunk.Type == (int)ChunkType.ContentUnit,
            VectorProperty = chunk => chunk.TextEmbedding
        };
        var results = new List<DocChunk>();
        const int topKChild = 5;
        var preResults = (await InternalSearchAsync(query, vectorSearchOptions, docId, token, topKChild,
            embeddingGenerator, collection)).ToList();
        while (level-- > 0 && preResults.Count > 0)
        {
            var searchResults = preResults.ToArray();
            preResults.Clear();
            foreach (var chunk in searchResults)
            {
                var record = chunk.Record;
                results.Add(record);
                preResults.AddRange(await InternalSearchAsync(record.Summary, vectorSearchOptions, docId, token, topK,
                    embeddingGenerator, collection));
            }
        }

        return results.Take(topK);
    }

    private async Task<IEnumerable<DocChunk>> GeneralSearchAsync(string query, string docId, CancellationToken token,
        int topK = 5)
    {
        var vectorSearchOptions = new VectorSearchOptions<DocChunk>()
        {
            //只搜索没有graph节点的文档
            Filter = chunk => chunk.Type == (int)ChunkType.ContentUnit,
            VectorProperty = chunk => chunk.TextEmbedding
        };
        return (await InternalSearchAsync(query, vectorSearchOptions, docId, token, topK))
            .Select(chunk => chunk.Record);
    }

    private async Task<IList<VectorSearchResult<T>>> InternalSearchAsync<T>(
        string query, VectorSearchOptions<T> searchOptions, string collectionId,
        CancellationToken token, int topK = 6, IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        VectorStoreCollection<string, T>? docCollection = null) where T : class
    {
        docCollection ??= _kernel.GetRequiredService<VectorStore>().GetCollection<string, T>(collectionId);
        await docCollection.EnsureCollectionExistsAsync(token);
        embeddingGenerator ??= _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embedding = await embeddingGenerator.GenerateAsync(query, cancellationToken: token);
        return await docCollection.SearchAsync(
            embedding.Vector,
            options: searchOptions,
            top: topK,
            cancellationToken: token).ToArrayAsync(cancellationToken: token);
    }

    private async Task PopulateParagraphs(VectorStoreCollection<string, DocChunk> collection, ChunkNode chunkNode)
    {
        var chunkKey = chunkNode.Chunk.Key;
        if (chunkNode.Chunk.Type == (int)ChunkType.ContentUnit)
        {
            return;
        }

        await foreach (var docChunk in collection.GetAsync(chunk => chunk.ParentKey == chunkKey, int.MaxValue / 2))
        {
            chunkNode.AddChild(new ChunkNode(docChunk));
        }
    }

    private async Task AddToRootNodes(IList<ChunkNode> rootNodes,
        IDictionary<string, ChunkNode> cache,
        VectorStoreCollection<string, DocChunk> store, ChunkNode chunkNode)
    {
        var currentNode = chunkNode;
        var parentKey = chunkNode.Chunk.ParentKey;
        while (!string.IsNullOrEmpty(parentKey))
        {
            if (cache.TryGetValue(parentKey, out var parentNode))
            {
                parentNode.AddChild(currentNode);
                return;
            }

            var parentChunk = await store.GetAsync(parentKey);
            if (parentChunk != null)
            {
                cache[parentKey] = parentNode = new ChunkNode(parentChunk);
                parentNode.AddChild(currentNode);
                parentKey = parentChunk.ParentKey;
                currentNode = parentNode;
            }
            else
            {
                break;
            }
        }

        rootNodes.Add(currentNode);
    }

    private VectorStore GetVectorStore()
    {
        return _kernel.GetRequiredService<VectorStore>();
    }

    private VectorStoreCollection<string, DocChunk> GetDocCollection(string collectionId)
    {
        //都是singleton，可以不关注生命周期
        var vectorStore = GetVectorStore();
        return vectorStore.GetCollection<string, DocChunk>(collectionId);
    }
}