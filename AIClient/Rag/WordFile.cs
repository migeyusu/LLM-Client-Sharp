using System.IO;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using Microsoft.SemanticKernel;

namespace LLMClient.Rag;

public class WordFile : RagFileBase
{
    [JsonConstructor]
    public WordFile()
    {
    }

    public WordFile(FileInfo fileInfo) : base(fileInfo)
    {
    }

    public override DocumentFileType FileType
    {
        get { return DocumentFileType.Word; }
    }

    public override Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<ISearchResult> QueryAsync(string query, dynamic options,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<ISearchResult> GetStructureAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<ISearchResult> GetSectionAsync(string sectionName,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<ISearchResult> GetFullDocumentAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override KernelFunctionFromMethodOptions QueryOptions
    {
        get { return new KernelFunctionFromMethodOptions(); }
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}