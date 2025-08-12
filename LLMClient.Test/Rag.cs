using System.Diagnostics.CodeAnalysis;
using System.Windows;
using LLMClient.Rag;
using LLMClient.Rag.Document;
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
            app.Run(new PDFExtractorWindow(new PDFExtractor(pdfPath)));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public async Task PDFExtractor()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize();
        pdfExtractor.Analyze();
    }

    [Fact]
    public async Task PDFEmbedding()
    {
        var pdfExtractor = new PDFExtractor(pdfPath);
        pdfExtractor.Initialize(new Thickness(10, 55, 10, 62));
        var contentNodes = pdfExtractor.Analyze();
        var docChunks =
            await contentNodes.ToSKDocChunks("doc1",((s, token) => Task.FromResult(s)), CancellationToken.None);
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
        var docCollection = requiredService.GetCollection<string, SKDocChunk>("test");
        await docCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        await docCollection.UpsertAsync(new SKDocChunk
        {
            Key = "chunk0",
            Text = "[1, 1, 0]",
            DocumentId = "doc0",
            Summary = "[1, 1, 0]",
            Title = "Title 0",
            Level = 0,
            ParentKey = String.Empty,
            HasChild = false
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
        var docCollection = requiredService.GetCollection<string, SKDocChunk>("test");
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
        var docCollection = requiredService.GetCollection<string, SKDocChunk>("test");
        await docCollection.EnsureCollectionExistsAsync(CancellationToken.None);
        await foreach (var result in docCollection.SearchAsync("1", 10,new VectorSearchOptions<SKDocChunk>()
                       {
                           VectorProperty = chunk => chunk.TextEmbedding
                       }))
        {
            output.WriteLine(result.Record.Text);
        }
    }
}

sealed class FakeEmbeddingGenerator(int? replaceLast = null) : IEmbeddingGenerator<string, Embedding<float>>
{
    float[] embeddings = Enumerable.Repeat(1f, 1536).ToArray();
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new GeneratedEmbeddings<Embedding<float>>();
        var count = values.Count();
        for (int i = 0; i < count; i++)
        {
            results.Add(new Embedding<float>(embeddings));
        }
        /*foreach (var value in values)
        {
            var vector = value.TrimStart('[').TrimEnd(']').Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            if (replaceLast is not null)
            {
                vector[^1] = replaceLast.Value;
            }
            results.Add(new Embedding<float>(vector));
        }*/
        return Task.FromResult(results);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => null;

    public void Dispose()
    {
    }
}