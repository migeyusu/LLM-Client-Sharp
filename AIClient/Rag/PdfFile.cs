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

    public override Task LoadAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        //删除存储库
        throw new NotImplementedException();
    }

    Func<string, Task<string>> CreateLLMCall(ILLMClient client, int summarySize = 100)
    {
        return async (content) =>
        {
            var stringBuilder = new StringBuilder("请为以下内容生成一个简短的摘要，要求：\r\n" +
                                                  "1. 摘要使用的语言和原文一致。\r\n" +
                                                  "2. 摘要长度不超过" + summarySize + "个字。\r\n" +
                                                  "3. 摘要内容应包含原文的主要信息。\r\n" +
                                                  "4. 摘要应尽量简洁明了。\r\n");
            stringBuilder.Append(content);
            var dialogContext = new DialogContext(new[]
            {
                new RequestViewItem() { TextMessage = stringBuilder.ToString(), }
            });
            var response = await client.SendRequest(dialogContext);
            var textResponse = response.TextResponse;
            if (string.IsNullOrEmpty(textResponse))
            {
                throw new InvalidOperationException("LLM response is empty.");
            }

            return textResponse;
        };
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
                await contentNodes.ToSKDocChunks(this.Id.ToString(), CreateLLMCall(digestClient), cancellationToken);
            var semanticKernelStore = new SemanticKernelStore(dbConnection);
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