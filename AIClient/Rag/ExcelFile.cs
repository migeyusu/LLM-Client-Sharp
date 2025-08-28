using System.IO;
using LLMClient.Abstraction;
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