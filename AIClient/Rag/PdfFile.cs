using System.IO;
using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Rag.Document;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.Logging;
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
        var semanticKernelStore = await GetStore();
        await semanticKernelStore.RemoveFile(this.DocumentId, cancellationToken);
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
            var docChunks = await pdfExtractorWindow.ContentNodes
                .ToSKDocChunks(this.DocumentId, CreateLLMCall(digestClient, 512, 3, this.ConstructionLogs),
                    this.ConstructionLogs, token: cancellationToken, nodeProgress: progress);
            var semanticKernelStore = await GetStore(ragOption);
            await semanticKernelStore.AddFile(this.DocumentId, docChunks, cancellationToken);
        }
    }

    private static async Task<SemanticKernelStore> GetStore(RagOption? ragOption = null)
    {
        ragOption ??= (await GlobalOptions.LoadOrCreate()).RagOption;
        ragOption.ThrowIfNotValid();
        var dbConnection = ragOption.DBConnection;
        var embeddingEndpoint = ragOption.EmbeddingEndpoint;
        if (embeddingEndpoint == null)
        {
            throw new InvalidOperationException("Embedding endpoint is not set.");
        }
#pragma warning disable SKEXP0001
        return new SemanticKernelStore().InitializeKernel(embeddingEndpoint,
            ragOption.EmbeddingModelId ?? "text-embedding-v3", dbConnection);
#pragma warning restore SKEXP0001
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

        var semanticKernelStore = await GetStore();
        IEnumerable<string> matchResult = await semanticKernelStore.SearchAsync(query, this.DocumentId,
            options.SearchAlgorithm ?? SemanticKernelStore.SearchAlgorithm.Default,
            options.TopK ?? 5, cancellationToken);
        return new SimpleQueryResult(this.DocumentId, matchResult.ToArray());
    }
}