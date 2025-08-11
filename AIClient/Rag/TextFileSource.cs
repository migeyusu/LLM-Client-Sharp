using System.IO;
using Newtonsoft.Json;

namespace LLMClient.Rag;

public class TextFile : RagFileBase
{
    [JsonConstructor]
    public TextFile()
    {
    }

    public TextFile(FileInfo fileInfo) : base(fileInfo)
    {
    }

    public override DocumentFileType FileType
    {
        get { return DocumentFileType.Text; }
    }

    public override Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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