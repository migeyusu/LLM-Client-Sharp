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
    private readonly PDFExtractor _pdfExtractor;

    private Thickness margin;

    [JsonConstructor]
    public PdfFile()
    {
    }


    public PdfFile(FileInfo fileInfo) : base(fileInfo)
    {
        var pdfExtractor = new PDFExtractor(fileInfo.FullName);
        var pdfExtractorWindow = new PDFExtractorWindow(pdfExtractor);
        if (pdfExtractorWindow.ShowDialog() == true)
        {
            margin = pdfExtractorWindow.FileMargin;
        }
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

    protected override async Task ConstructCore(CancellationToken cancellationToken = default)
    {
        /*if (_pdfExtractor == null)
        {
            throw new InvalidOperationException("PDFExtractor is not initialized.");
        }

        await Task.Yield();
        _pdfExtractor.Initialize(margin);
        var ragOption = (await GlobalOptions.LoadOrCreate()).RagOption;
        ragOption.DBConnection;

        var contentNodes = _pdfExtractor.Analyze();
        var docChunks = await contentNodes.ToSKDocChunks(this.Id.ToString(),CreateLLMCall());
        var semanticKernelStore = new SemanticKernelStore();
        await semanticKernelStore.AddFileToContext(docChunks, cancellationToken);*/
    }

    public override Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}