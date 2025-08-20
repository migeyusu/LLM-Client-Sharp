using System.IO;
using LLMClient.Data;
using LLMClient.Rag.Document;
using LLMClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Newtonsoft.Json;

namespace LLMClient.Rag;

public class PdfFile : RagFileBase
{
    [JsonConstructor]
    public PdfFile()
    {
    }

    public PdfFile(FileInfo fileInfo) : base(fileInfo)
    {
    }

    public override DocumentFileType FileType
    {
        get { return DocumentFileType.Pdf; }
    }

    public override async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        /*if (this.Status == ConstructStatus.Constructed)
        {
            var kernelStore = await GetStore();
            if (!await kernelStore.CheckConnection())
            {
                return;
            }
        }*/

        if (this.Status == ConstructStatus.Constructing)
        {
            //说明上次构建还未完成，需要删除之前的脏数据重新构建。
            this.Status = ConstructStatus.Error;
            this.ErrorMessage = "上次构建未完成，请重新构建。";
            // await this.DeleteAsync();
        }

        IsInitialized = true;
    }

    public override async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var semanticKernelStore = await GetStoreAsync();
        await semanticKernelStore.RemoveFileAsync(this.DocumentId, cancellationToken);
        this.Status = ConstructStatus.NotConstructed;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async Task ConstructCore(CancellationToken cancellationToken = default)
    {
        using (var pdfExtractor = new PDFExtractor(FilePath))
        {
            var pdfExtractorWindow = new PDFExtractorWindow(pdfExtractor);
            if (pdfExtractorWindow.ShowDialog() != true)
            {
                throw new InvalidOperationException("PDF extraction was cancelled by the user.");
            }

            await Task.Yield();
            var ragOption = (await GlobalOptions.LoadOrCreate()).RagOption;
            var digestClient = ragOption.DigestClient;
            if (digestClient == null)
            {
                throw new InvalidOperationException("Digest client is not set.");
            }

            var progress = new Progress<PDFContentNode>(node =>
            {
                // 会自动在UI线程调用
                ConstructionLogs.LogInformation("Processing node {0}, start page: {1}, level: {2}",
                    node.Title, node.StartPage, node.Level);
            });
            using (var semaphoreSlim = new SemaphoreSlim(5, 5))
            {
                var promptsCache = new PromptsCache(this.Id.ToString(), PromptsCache.CacheFolderPath,
                    digestClient.Endpoint.Name, digestClient.Model.Id) { OutputSize = SummarySize };
                await promptsCache.InitializeAsync();
                SemanticKernelStore? store = null;
                try
                {
                    var docChunks = await pdfExtractorWindow.ContentNodes
                        .ToDocChunks(this.DocumentId,
                            CreateLLMCall(digestClient, semaphoreSlim, promptsCache, SummarySize, 3,
                                this.ConstructionLogs),
                            logger: this.ConstructionLogs, nodeProgress: progress, token: cancellationToken);
                    this.ConstructionLogs.LogInformation("PDF extraction completed, total chunks: {0}",
                        docChunks.Count);
                    store = await GetStoreAsync(ragOption);
                    this.ConstructionLogs.LogInformation("Saving to vector store.");
                    await store.AddFile(this.DocumentId, docChunks, cancellationToken);
                }
                catch (Exception)
                {
                    if (store != null)
                    {
                        await store.RemoveFileAsync(this.DocumentId, cancellationToken);
                    }

                    throw;
                }
                finally
                {
                    await promptsCache.SaveAsync();
                }
            }
        }
    }

    private static async Task<SemanticKernelStore> GetStoreAsync(RagOption? ragOption = null)
    {
        ragOption ??= (await GlobalOptions.LoadOrCreate()).RagOption;
        ragOption.ThrowIfNotValid();
        return ragOption.GetStore();
    }

    public override async Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "The PDF file has not been initialized. Please call InitializeAsync first.");
        }

        if (Status != ConstructStatus.Constructed)
        {
            throw new InvalidOperationException(
                "The PDF file has not been constructed. Please call ConstructAsync first.");
        }

        var semanticKernelStore = await GetStoreAsync();
        IList<ChunkNode> matchResult = await semanticKernelStore.SearchAsync(query, this.DocumentId,
            options.SearchAlgorithm ?? SearchAlgorithm.Default,
            options.TopK ?? 5, cancellationToken);
        return new StructResult(matchResult);
    }

    public override async Task<ISearchResult> GetStructureAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "The PDF file has not been initialized. Please call InitializeAsync first.");
        }

        if (Status != ConstructStatus.Constructed)
        {
            throw new InvalidOperationException(
                "The PDF file has not been constructed. Please call ConstructAsync first.");
        }

        var semanticKernelStore = await GetStoreAsync();
        var structureNodes = await semanticKernelStore.GetStructureAsync(this.DocumentId, cancellationToken);
        return new StructResult(structureNodes) { DocumentId = this.DocumentId };
    }

    public override async Task<ISearchResult> GetSectionAsync(string titleName,
        CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync();
        var chunkNode = await store.GetSectionAsync(this.DocumentId, titleName, cancellationToken);
        return new StructResult(chunkNode == null ? Array.Empty<ChunkNode>() : new[] { chunkNode })
            { DocumentId = this.DocumentId };
    }

    public override async Task<ISearchResult> GetFullDocumentAsync(CancellationToken cancellationToken = default)
    {
        var kernelStore = await GetStoreAsync();
        var nodes = await kernelStore.GetDocTreeAsync(this.DocumentId, cancellationToken);
        return new StructResult(nodes) { DocumentId = this.DocumentId };
    }

    private KernelFunctionFromMethodOptions? _queryOptions;

    protected override KernelFunctionFromMethodOptions QueryOptions =>
        _queryOptions ??= new()
        {
            FunctionName = "Search",
            Description =
                "Performs a RAG (Retrieval-Augmented Generation) search on the PDF document. This function first converts the user query into a vector embedding, " +
                "then searches the document content in a vector database by calculating cosine similarity to find the most relevant parts. " +
                "Returns the document sections or paragraphs that match the query, which can be used to answer user questions or generate summaries of related content." +
                "\n" +
                "Note: The query is not a full-text search but a vector search based on the embedding of the query. " +
                "So first you should generate a appropriate query that is related to the content you want to search." +
                "\n" +
                "Sample: If you want to find how many steps in the process" +
                "you should not ask directly like 'How many steps in the process?'" +
                " but generate a query like 'steps in the process' or 'process steps' \n" +
                "Warning: it's better to get the structure of the document first before using this function",
            Parameters =
            [
                new KernelParameterMetadata("query")
                {
                    Description = "What to search for, be aware that the query is not a full text search" +
                                  "but a vector search based on the embedding of the query." +
                                  "So you should not ask directly but generate a query that is related to the content you want to search.",
                    ParameterType = typeof(string),
                    IsRequired = true
                },

                new KernelParameterMetadata("TopK")
                {
                    Description = "Number of results most relevant to the query to return. " +
                                  "This is the number of chunks that will be returned, " +
                                  "not the number of paragraphs or sections.",
                    ParameterType = typeof(int),
                    IsRequired = false,
                    DefaultValue = 2
                },

                new KernelParameterMetadata("SearchAlgorithm")
                {
                    Description = "Select search algorithm to use." +
                                  Extension.GenerateEnumDescription(typeof(SearchAlgorithm)),
                    ParameterType = typeof(SearchAlgorithm),
                    IsRequired = false,
                    DefaultValue = 0
                },
            ],

            ReturnParameter = new()
            {
                ParameterType = typeof(KernelSearchResults<string>),
                Description =
                    "The search results, which will be formatted as a tree view:(so the view will only contains incomplete structure.)\n" +
                    "- Title 0\n  This is a paragraph.\n  This is another paragraph.\n- Title 1\n  - Title 1.2\n    This is a paragraph under Title 1.2.\n    This is another paragraph under Title 1.2."
            },
        };
}