/*using System.IO;
using System.Windows.Forms;
using LLMClient.Endpoints.OpenAIAPI;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Handlers;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Memory;

namespace LLMClient.Rag;

public class KMRag
{
    public void Test()
    {
        var memoryBuilder = new KernelMemoryBuilder()
            .WithoutDefaultHandlers() // remove default handlers, added manually below
            .WithOpenAIDefaults(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
            .WithCustomSearchClient<HierarchicalSearchClient>()  ;

        var memory = memoryBuilder.Build<MemoryServerless>();

/*********************************************************
 * Define custom handlers
 ********************************************************#1#

        Console.WriteLine("* Registering pipeline handlers...");
        
// 注册自定义解码器  
        memory.Orchestrator.AddHandler<TreeStructurePdfDecoder>("extract");  
        memory.Orchestrator.AddHandler<StructureAwarePartitioningHandler>("partition");  
        memory.Orchestrator.AddHandler<CustomSaveRecordsHandler>("save_records");
  
// 使用自定义管道导入文档  
        await memory.ImportDocumentAsync(  
            new Document("pdf-with-structure")  
                .AddFile("document.pdf"),  
            steps: new[] { "extract", "partition", "gen_embeddings", "save_records" });
        memory.Orchestrator.AddHandler<TextExtractionHandler>("extract_text");
        memory.Orchestrator.AddHandler<TextPartitioningHandler>("split_text_in_partitions");
        memory.Orchestrator.AddHandler<GenerateEmbeddingsHandler>("generate_embeddings");
        memory.Orchestrator.AddHandler<SummarizationHandler>("summarize");
        memory.Orchestrator.AddHandler<SaveRecordsHandler>("save_memory_records");

/*********************************************************
 * Import files using custom handlers
 ********************************************************#1#

// Use the custom handlers with the memory object
        await memory.ImportDocumentAsync(
            new Document("inProcessTest")
                .AddFile("file1-Wikipedia-Carbon.txt")
                .AddFile("file2-Wikipedia-Moon.txt")
                .AddFile("file3-lorem-ipsum.docx")
                .AddFile("file4-KM-Readme.pdf")
                .AddFile("file5-NASA-news.pdf")
                .AddTag("testName", "example3"),
            index: "user-id-1",
            steps:
            [
                "extract_text",
                "split_text_in_partitions",
                "generate_embeddings",
                "save_memory_records"
            ]);
        
        var results = await memory.SearchAsync("查询内容",   
            filter: MemoryFilters.ByTag("structure_level", "2"));  
    }
    
    public async Task<MemoryAnswer> AskAsync(  
        string index,  
        string question,  
        ICollection<MemoryFilter>? filters = null,  
        double minRelevance = 0,  
        IContext? context = null,  
        CancellationToken cancellationToken = default)  
    {  
        // 1. 先搜索最相关的具体内容  
        var detailedResults = await SearchByRelevance(index, question, filters, minRelevance);  
      
        // 2. 为每个结果获取其上下文（父级和兄弟节点）  
        var contextualResults = new List<MemoryRecord>();  
        foreach (var result in detailedResults)  
        {  
            // 获取父级上下文  
            var parentContext = await GetParentContext(index, result);  
            if (parentContext != null) contextualResults.Add(parentContext);  
          
            // 添加当前结果  
            contextualResults.Add(result);  
          
            // 获取相关兄弟节点  
            var siblings = await GetSiblingContext(index, result);  
            contextualResults.AddRange(siblings);  
        }  
      
        // 3. 使用增强的上下文生成答案  
        return await GenerateContextualAnswer(question, contextualResults, context, cancellationToken);  
    }
    
    private async Task<MemoryRecord?> GetParentContext(string index, MemoryRecord record)  
    {  
        var parentId = record.GetTagValue("parent_chunk_id");  
        if (string.IsNullOrEmpty(parentId)) return null;  
  
        var parentFilters = new List<MemoryFilter>  
        {  
            MemoryFilters.ByTag(Constants.ReservedFilePartitionTag, parentId)  
        };  
  
        var parents = _memoryDb.GetListAsync(index, parentFilters, 1, false);  
        await foreach (var parent in parents)  
        {  
            return parent;  
        }  
      
        return null;  
    }
    
    private List<Citation> RankByStructuralRelevance(List<Citation> results, string query)  
    {  
        return results  
            .OrderBy(r => GetStructureLevel(r))  // 优先显示高层级内容  
            .ThenByDescending(r => r.Relevance)  // 然后按相关度排序  
            .ThenBy(r => GetStructurePath(r))    // 最后按结构路径排序  
            .ToList();  
    }
}

public class TreeStructurePdfDecoder : IContentDecoder  
{  
    public bool SupportsMimeType(string mimeType)  
    {  
        return mimeType?.StartsWith(Microsoft.KernelMemory.Pipeline.MimeTypes.Pdf, StringComparison.OrdinalIgnoreCase) == true;  
    }

    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = new CancellationToken())
    {
        using var stream = File.OpenRead(filename);
        return this.DecodeAsync(stream, cancellationToken);
    }

    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = new CancellationToken())
    {
        using var stream = data.ToStream();
        return this.DecodeAsync(stream, cancellationToken);
    }

    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)  
    {  
        // 实现您的PDF树形结构解析逻辑  
        var result = new FileContent(Microsoft.KernelMemory.Pipeline.MimeTypes.PlainText);  
          
        // 解析PDF并构建树形结构  
        var treeStructure = ParsePdfToTree(data);  
          
        // 将树形结构转换为带有结构信息的sections  
        foreach (var node in treeStructure)  
        {  
            var chunk = new Chunk(  
                node.Content,   
                node.PageNumber,   
                CreateMetaWithStructure(node)  
            );  
            result.Sections.Add(chunk);  
        }  
          
        return Task.FromResult(result);  
    }  
      
    private TreeNode ParsePdfToTree(Stream pdfData)  
    {  
        // 实现您的树形结构解析逻辑  
        // 可以基于PDF的书签、标题层级、段落结构等  
    }  
      
    private Dictionary<string, object> CreateMetaWithStructure(TreeNode node)  
    {  
        return new Dictionary<string, object>  
        {  
            ["level"] = node.Level,  
            ["parent_id"] = node.ParentId,  
            ["section_type"] = node.SectionType,  
            ["structure_path"] = node.StructurePath  
        };  
    }  
}


public class HybridPartitioningHandler : IPipelineStepHandler  
{  
    private readonly TextPartitioningHandler _defaultHandler;  
    private readonly StructureAwarePartitioningHandler _pdfHandler;  
  
    public async Task<(ReturnType, DataPipeline)> InvokeAsync(  
        DataPipeline pipeline, CancellationToken cancellationToken = default)  
    {  
        // 检查是否包含PDF文件  
        bool hasPdfFiles = pipeline.Files.Any(f =>   
            f.GeneratedFiles.Values.Any(gf =>   
                gf.MimeType == MimeTypes.Pdf &&   
                gf.ArtifactType == DataPipeline.ArtifactTypes.ExtractedText));  
  
        if (hasPdfFiles)  
        {  
            // 使用自定义PDF处理逻辑  
            return await _pdfHandler.InvokeAsync(pipeline, cancellationToken);  
        }  
        else  
        {  
            // 使用默认处理逻辑  
            return await _defaultHandler.InvokeAsync(pipeline, cancellationToken);  
        }  
    }  
}

public class StructureAwarePartitioningHandler : IPipelineStepHandler
{  
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(  
        DataPipeline pipeline, CancellationToken cancellationToken = default)  
    {  
        foreach (var file in pipeline.Files)  
        {  
            foreach (var generatedFile in file.GeneratedFiles.Values)  
            {  
                if (generatedFile.ArtifactType == DataPipeline.ArtifactTypes.ExtractedText)  
                {  
                    // 读取带有结构信息的内容  
                    var content = await ReadFileWithStructure(pipeline, generatedFile);  
                      
                    // 基于树形结构进行智能分块  
                    var structuredChunks = CreateStructureAwareChunks(content);  
                      
                    // 保存分块结果  
                    await SaveStructuredChunks(pipeline, structuredChunks);  
                }  
            }  
        }  
          
        return (true, pipeline);  
    }

    public string StepName => "StructureAwarePartitioningHandler";
    
   
    private static MemoryRecord PrepareStructuredRecord(  
        DataPipeline pipeline,  
        string recordId,  
        StructuredChunk chunk,
        // ... 其他参数  
    )  
    {  
        var record = new MemoryRecord { Id = recordId };  
      
        // 添加标准字段  
        record.Tags.Add(Constants.ReservedDocumentIdTag, pipeline.DocumentId);  
      
        // 添加结构信息  
        record.Tags.Add("structure_level", chunk.Level.ToString());  
        record.Tags.Add("parent_chunk_id", chunk.ParentId);  
        record.Tags.Add("section_type", chunk.SectionType);  
      
        // 在payload中保存详细的结构路径  
        record.Payload["structure_path"] = chunk.StructurePath;  
        record.Payload["tree_position"] = chunk.TreePosition;  
      
        return record;  
    }
}


public class HierarchicalSearchClient : ISearchClient  
{  
    private readonly IMemoryDb _memoryDb;  
    private readonly ITextEmbeddingGenerator _embeddingGenerator;  
    private readonly ILogger<HierarchicalSearchClient> _log;  
  
    public async Task<SearchResult> SearchAsync(  
        string index,  
        string query,  
        ICollection<MemoryFilter>? filters = null,  
        double minRelevance = 0,  
        int limit = -1,  
        IContext? context = null,  
        CancellationToken cancellationToken = default)  
    {  
        // 1. 首先搜索顶层结构（level=0或1）  
        var topLevelFilters = new List<MemoryFilter>(filters ?? [])  
        {  
            MemoryFilters.ByTag("structure_level", "0"),  
            MemoryFilters.ByTag("structure_level", "1")  
        };  
  
        var topLevelResults = await SearchByLevel(  
            index, query, topLevelFilters, minRelevance, limit, cancellationToken);  
  
        // 2. 基于顶层结果，递归搜索子级内容  
        var hierarchicalResults = new List<Citation>();  
        foreach (var topResult in topLevelResults)  
        {  
            hierarchicalResults.Add(topResult);  
              
            // 获取子级内容  
            var childResults = await SearchChildrenRecursively(  
                index, query, topResult, minRelevance, cancellationToken);  
            hierarchicalResults.AddRange(childResults);  
        }  
  
        return new SearchResult  
        {  
            Query = query,  
            Results = hierarchicalResults.Take(limit).ToList()  
        };  
    }  
  
    private async Task<List<Citation>> SearchChildrenRecursively(  
        string index,   
        string query,   
        Citation parentResult,  
        double minRelevance,  
        CancellationToken cancellationToken)  
    {  
        var results = new List<Citation>();  
          
        // 搜索直接子级  
        var childFilters = new List<MemoryFilter>  
        {  
            MemoryFilters.ByTag("parent_chunk_id", parentResult.SourceContentType) // 使用父级ID  
        };  
  
        var childMatches = _memoryDb.GetSimilarListAsync(  
            index, query, childFilters, minRelevance, 10, false, cancellationToken);  
  
        await foreach (var (record, relevance) in childMatches)  
        {  
            var citation = CreateCitationFromRecord(record, relevance);  
            results.Add(citation);  
  
            // 递归搜索更深层级  
            var grandChildren = await SearchChildrenRecursively(  
                index, query, citation, minRelevance, cancellationToken);  
            results.AddRange(grandChildren);  
        }  
  
        return results;  
    }  
}*/