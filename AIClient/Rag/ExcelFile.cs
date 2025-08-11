using System.IO;
using Newtonsoft.Json;

namespace LLMClient.Rag;

public class ExcelFile : RagFileBase
{
    [JsonConstructor]
    public ExcelFile()
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

    public override Task<ISearchResult> QueryAsync(string query, dynamic options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}