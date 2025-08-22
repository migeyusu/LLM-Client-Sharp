using System.IO;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;

namespace LLMClient.Rag;

public class ExcelFile : RagFileBase
{
    [JsonConstructor]
    public ExcelFile() : base()
    {
    }

    public ExcelFile(FileInfo fileInfo) : base(fileInfo)
    {
    }

    public override DocumentFileType FileType
    {
        get { return DocumentFileType.Excel; }
    }

    public override Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
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
        get
        {
            return new KernelFunctionFromMethodOptions();
        }
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}