using LLMClient.Data;
using LLMClient.Rag.Document;
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

    public override DocumentFileType FileType => DocumentFileType.Pdf;

    public override Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return Task.CompletedTask;
        }

        /*if (this.Status == ConstructStatus.Constructed)
        {
            var kernelStore = await GetStore();
            if (!await kernelStore.CheckConnection())
            {
                return;
            }
        }*/

        if (this.Status == RagStatus.Constructing)
        {
            //说明上次构建还未完成，需要删除之前的脏数据重新构建。
            this.Status = RagStatus.Error;
            this.ErrorMessage = "上次构建未完成，请重新构建。";
            // await this.DeleteAsync();
        }

        IsInitialized = true;
        return Task.CompletedTask;
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
            var promptsCache = new PromptsCache(this.DocumentId, PromptsCache.CacheFolderPath);
            var pdfExtractorViewModel = new PDFExtractorViewModel(pdfExtractor, RagOption, promptsCache);
            var pdfExtractorWindow = new PDFExtractorWindow(pdfExtractorViewModel);
            if (pdfExtractorWindow.ShowDialog() != true)
            {
                throw new InvalidOperationException("PDF extraction was cancelled by the user.");
            }

            await promptsCache.SaveAsync();
            // await Task.Yield();
            var docChunks = await pdfExtractorViewModel.ContentNodes
                .ToDocChunks<PDFNode, PDFPage>(this.DocumentId, logger: this.ConstructionLogs);
            this.ConstructionLogs.LogInformation("PDF extraction completed, total chunks: {0}",
                docChunks.Count);
            var store = GetStore();
            try
            {
                this.ConstructionLogs.LogInformation("Saving to vector store.");
                await store.AddFile(this.DocumentId, docChunks, cancellationToken);
            }
            catch (Exception)
            {
                await store.RemoveFileAsync(this.DocumentId, CancellationToken.None);
                throw;
            }
        }
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