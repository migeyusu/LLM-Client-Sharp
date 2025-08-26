using System.IO;
using System.Text.Json.Serialization;
using LLMClient.Rag.Document;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LLMClient.Rag;

public class MarkdownFile : RagFileBase
{
    [JsonConstructor]
    public MarkdownFile()
    {
    }

    public MarkdownFile(FileInfo fileInfo) : base(fileInfo)
    {
    }

    public override DocumentFileType FileType => DocumentFileType.Markdown;

    protected override KernelFunctionFromMethodOptions QueryOptions
    {
        get { return new KernelFunctionFromMethodOptions(); }
    }

    protected override async Task ConstructCore(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            throw new FileNotFoundException("Markdown file not found.", FilePath);
        }

        var markdownExtractorWindow = new MarkdownExtractorWindow(FilePath, RagOption);
        if (markdownExtractorWindow.ShowDialog() != true)
        {
            throw new InvalidOperationException("Markdown extraction was cancelled by the user.");
        }

        var docChunks = await markdownExtractorWindow.ContentNodes
            .ToDocChunks<MarkdownNode, MarkdownText>(this.DocumentId);
        SemanticKernelStore? store = null;
        this.ConstructionLogs.LogInformation("PDF extraction completed, total chunks: {0}",
            docChunks.Count);
        store = GetStore();
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