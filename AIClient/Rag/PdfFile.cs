using System.IO;
using System.Text;
using System.Windows;
using LLMClient.Abstraction;
using LLMClient.Dialog;
using LLMClient.Rag.Document;
using LLMClient.UI;
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

    public override Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        //删除存储库
        throw new NotImplementedException();
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
            Thickness margin;
            if (pdfExtractorWindow.ShowDialog() == true)
            {
                margin = pdfExtractorWindow.FileMargin;
            }

            await Task.Yield();
            pdfExtractor.Initialize(margin);
            var ragOption = (await GlobalOptions.LoadOrCreate()).RagOption;
            ragOption.ThrowIfNotValid();
            var dbConnection = ragOption.DBConnection;
            var digestClient = ragOption.DigestClient;
            if (digestClient == null)
            {
                throw new InvalidOperationException("Digest client is not set.");
            }

            var embeddingClient = ragOption.EmbeddingClient;
            if (embeddingClient == null)
            {
                throw new InvalidOperationException("Embedding client is not set.");
            }

            var contentNodes = pdfExtractor.Analyze();
            var docChunks =
                await contentNodes.ToSKDocChunks(this.DocumentId, CreateLLMCall(digestClient), cancellationToken);
            var semanticKernelStore = new SemanticKernelStore(dbConnection);
            semanticKernelStore.InitializeKernel(embeddingClient, ragOption.EmbeddingModelId);
            await semanticKernelStore.AddFile(this.DocumentId, docChunks, cancellationToken);
        }
    }

    public override async Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default)
    {
        var ragOption = (await GlobalOptions.LoadOrCreate()).RagOption;
        ragOption.ThrowIfNotValid();
        var dbConnection = ragOption.DBConnection;
        var semanticKernelStore = new SemanticKernelStore(dbConnection);
        IEnumerable<string> matchResult = await semanticKernelStore.SearchAsync(query, this.DocumentId,
            options.SearchAlgorithm ?? SemanticKernelStore.SearchAlgorithm.Default,
            options.TopK ?? 5, cancellationToken);
        return new SimpleQueryResult(this.DocumentId, matchResult.ToArray());
    }
}