using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using LLMClient.Data;
using LLMClient.Rag;
using LLMClient.Rag.Document;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class Rag
{
    private ITestOutputHelper output;

    const string pdfPath =
        @"C:\Users\jie.zhu\Documents\WXWork\1688854281599012\Cache\File\2025-07\AMT_M1A0_Datasheet_v0p5_250428.pdf";

    private const string MarkDownPath = @"E:\Doc\rag_doc\intel-64-architecture-processor-topology-enumeration\full.md";

    public Rag(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void PDFClip()
    {
        var thread = new Thread(o =>
        {
            var app = new App();
            app.InitializeComponent();
            var pdfExtractorViewModel = new PDFExtractorViewModel(
                new PDFExtractor(pdfPath), new RagOption(),
                PromptsCache.NoCache);
            app.Run(new PDFExtractorWindow(pdfExtractorViewModel));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void PDFExtractor()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize();
        pdfExtractor.Analyze();
    }

    [Fact]
    public async Task PDFEmbedding()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize(padding: new Thickness(10, 55, 10, 62));
        var contentNodes = pdfExtractor.Analyze();
        var docChunks = await contentNodes.ToDocChunks<PDFNode, PDFPage>("doc1");
        var docChunksCount = docChunks.Count;
        output.WriteLine(docChunksCount.ToString());
    }

    [Fact]
    [Experimental("SKEXP0010")]
    public async Task DataStore()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services
            .AddEmbeddingGenerator(new FakeEmbeddingGenerator());
        // .AddOpenAIEmbeddingGenerator("test", new OpenAIClient("xxxxx"))
        // 添加 SQLite 向量存储（连接字符串指向本地文件）
        kernelBuilder.Services.AddSqliteVectorStore(connectionStringProvider: sp => "Data Source=mydatabase.db",
            (provider =>
                new SqliteVectorStoreOptions()
                {
                    EmbeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
                })); // 自动创建 db 文件
        var kernel = kernelBuilder.Build();
        var requiredService = kernel.GetRequiredService<VectorStore>();
        var docCollection = requiredService.GetCollection<string, DocChunk>("test");
        await docCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        await docCollection.UpsertAsync(new DocChunk
        {
            Key = "chunk0",
            Text = "[1, 1, 0]",
            DocumentId = "doc0",
            Summary = "[1, 1, 0]",
            Title = "Title 0",
            Level = 0,
            ParentKey = String.Empty,
            HasChildNode = false
        });
    }

    [Fact]
    public async Task DataLoad()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services
            .AddEmbeddingGenerator(new FakeEmbeddingGenerator());
        // .AddOpenAIEmbeddingGenerator("test", new OpenAIClient("xxxxx"))
        // 添加 SQLite 向量存储（连接字符串指向本地文件）
        kernelBuilder.Services.AddSqliteVectorStore(connectionStringProvider: sp => "Data Source=mydatabase.db",
            (provider =>
                new SqliteVectorStoreOptions()
                {
                    EmbeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
                })); // 自动创建 db 文件
        var kernel = kernelBuilder.Build();
        var requiredService = kernel.GetRequiredService<VectorStore>();
        await foreach (var s in requiredService.ListCollectionNamesAsync())
        {
            output.WriteLine(s);
        }
    }

    [Fact]
    public async Task DataDelete()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services
            .AddEmbeddingGenerator(new FakeEmbeddingGenerator());
        // .AddOpenAIEmbeddingGenerator("test", new OpenAIClient("xxxxx"))
        // 添加 SQLite 向量存储（连接字符串指向本地文件）
        kernelBuilder.Services.AddSqliteVectorStore(connectionStringProvider: sp => "Data Source=mydatabase.db",
            (provider =>
                new SqliteVectorStoreOptions()
                {
                    EmbeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
                })); // 自动创建 db 文件
        var kernel = kernelBuilder.Build();
        var requiredService = kernel.GetRequiredService<VectorStore>();
        var docCollection = requiredService.GetCollection<string, DocChunk>("test");
        await docCollection.EnsureCollectionDeletedAsync();
        // await requiredService.EnsureCollectionDeletedAsync("test", CancellationToken.None);
        var existsAsync = await requiredService.CollectionExistsAsync("test", CancellationToken.None);
        Assert.False(existsAsync, "Collection 'test' should not exist after deletion.");
    }

    [Fact]
    public async Task SearchAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services
            .AddEmbeddingGenerator(new FakeEmbeddingGenerator());
        kernelBuilder.Services.AddSqliteVectorStore(connectionStringProvider: sp => "Data Source=mydatabase.db",
            (provider =>
                new SqliteVectorStoreOptions()
                {
                    EmbeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
                })); // 自动创建 db 文件
        var kernel = kernelBuilder.Build();
        var requiredService = kernel.GetRequiredService<VectorStore>();
        var docCollection = requiredService.GetCollection<string, DocChunk>("test");
        await docCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        await foreach (var result in docCollection.SearchAsync("1", 10, new VectorSearchOptions<DocChunk>()
                       {
                           VectorProperty = chunk => chunk.TextEmbedding
                       }))
        {
            output.WriteLine(result.Record.Text);
        }
    }

    [Fact]
    public void ResultPrint()
    {
        List<ChunkNode> nodes = new List<ChunkNode>()
        {
            new ChunkNode(new DocChunk()
            {
                Title = "Title 0",
                Summary = "Summary 0",
                Type = (int)ChunkType.Node,
            })
            {
                Children =
                [
                    new ChunkNode(new DocChunk()
                    {
                        Type = (int)ChunkType.ContentUnit,
                        Text = "This is a paragraph.",
                        Summary = "This is a paragraph summary.",
                    }),
                    new ChunkNode(new DocChunk()
                    {
                        Type = (int)ChunkType.ContentUnit,
                        Text = "This is another paragraph.",
                        Summary = "This is another paragraph summary.",
                    })
                ]
            }
        };
        var chunkNode = new ChunkNode(new DocChunk()
        {
            Title = "Title 1",
            Summary = "Summary 1",
            Type = (int)ChunkType.Node,
            HasChildNode = true,
        })
        {
            Children =
            [
                new ChunkNode(new DocChunk()
                {
                    Title = "Title 1.1",
                    Summary = "Summary 1.1",
                    Type = (int)ChunkType.Node
                })
                {
                    Children =
                    [
                        new ChunkNode(new DocChunk()
                        {
                            Type = (int)ChunkType.ContentUnit,
                            Text = "This is a paragraph under Title 1.1.",
                            Summary = "This is a paragraph summary under Title 1.1."
                        }),
                        new ChunkNode(new DocChunk()
                        {
                            Type = (int)ChunkType.ContentUnit,
                            Text = "This is another paragraph under Title 1.1.",
                            Summary = "This is another paragraph summary under Title 1.1."
                        })
                    ]
                },
                new ChunkNode(new DocChunk()
                {
                    Title = "Title 1.2",
                    Summary = "Summary 1.2",
                    Type = (int)ChunkType.Node
                })
                {
                    Children =
                    [
                        new ChunkNode(new DocChunk()
                        {
                            Type = (int)ChunkType.ContentUnit,
                            Text = "This is a paragraph under Title 1.2.",
                            Summary = "This is a paragraph summary under Title 1.2."
                        }),
                        new ChunkNode(new DocChunk()
                        {
                            Type = (int)ChunkType.ContentUnit,
                            Text = "This is another paragraph under Title 1.2.",
                            Summary = "This is another paragraph summary under Title 1.2."
                        })
                    ]
                }
            ]
        };
        nodes.Add(chunkNode);
        nodes.Add(new ChunkNode(new DocChunk()
        {
            Title = "Title 2",
            Summary = "Summary 2",
            Type = (int)ChunkType.Node
        })
        {
            Children =
            [
                new ChunkNode(new DocChunk()
                {
                    Type = (int)ChunkType.ContentUnit,
                    Text = "This is a paragraph under Title 2.",
                    Summary = "This is a paragraph summary under Title 2."
                }),
                new ChunkNode(new DocChunk()
                {
                    Type = (int)ChunkType.ContentUnit,
                    Text = "This is another paragraph under Title 2.",
                    Summary = "This is another paragraph summary under Title 2."
                })
            ]
        });
        var structure = nodes.GetStructure();
        output.WriteLine(structure);
        var view = nodes.GetView();
        output.WriteLine(view);
    }

    [Fact]
    public async Task MarkdownToNodes()
    {
        var markdownNodes = await new MarkdownParser().Parse(MarkDownPath);
        // var markdownNodes = MarkdownParser.ParseFromFile(markDownPath);
        var docChunks = await markdownNodes.ToDocChunks<MarkdownNode, MarkdownText>("doc1");
        Debugger.Break();
    }
}