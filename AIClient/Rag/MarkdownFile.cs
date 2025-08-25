using System.IO;
using System.Text.Json.Serialization;
using LLMClient.Rag.Document;
using LLMClient.UI;
using LLMClient.UI.Render;
using Microsoft.Extensions.DependencyInjection;
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

        var markdownParser = new MarkdownParser();
        var markdownNodes = await markdownParser.Parse(FilePath);
        var docChunks = await markdownNodes.ToDocChunks<MarkdownNode,MarkdownContent>(this.DocumentId);
        var ragOption = ServiceLocator.GetService<GlobalOptions>()!.RagOption;
        SemanticKernelStore? store = null;
        this.ConstructionLogs.LogInformation("PDF extraction completed, total chunks: {0}",
            docChunks.Count);
        store = await GetStoreAsync(ragOption);
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