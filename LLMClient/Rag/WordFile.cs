using System.Text.Json.Serialization;
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

    protected override KernelFunctionFromMethodOptions QueryOptions
    {
        get { return new KernelFunctionFromMethodOptions(); }
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}