using System.IO;
using LLMClient.Abstraction;
using Microsoft.SemanticKernel;
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
    
    protected override KernelFunctionFromMethodOptions QueryOptions
    {
        get
        {
            return new KernelFunctionFromMethodOptions();
        }
    }

    protected override Task ConstructCore(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}