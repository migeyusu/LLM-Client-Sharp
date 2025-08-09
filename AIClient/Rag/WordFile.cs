using System.IO;
using System.Text.Json.Serialization;

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

    public override Task LoadAsync()
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override Task<ISearchResult> QueryAsync(string query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}