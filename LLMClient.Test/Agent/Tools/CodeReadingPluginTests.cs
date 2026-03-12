// CodeReadingPluginTests.cs

using System.Text.Json;
using LLMClient.ContextEngineering.Tools;
using LLMClient.ContextEngineering.Tools.Models;

namespace LLMClient.Test.Agent.Tools;

[Collection("CodeReading collection")]
public sealed class CodeReadingPluginTests
{
    private readonly CodeReadingFixture _fixture;
    private readonly CodeReadingPlugin _plugin;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CodeReadingPluginTests(CodeReadingFixture fixture)
    {
        _fixture = fixture;
        _plugin = fixture.Plugin;
    }

    [Fact]
    public void ReadFile_ReturnsJsonPayload()
    {
        var json = _plugin.ReadFile(
            _fixture.RelativeSourcePath,
            startLine: 1,
            endLine: 3);

        var result = JsonSerializer.Deserialize<ReadFileResult>(json, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result!.StartLine);
        Assert.Equal(3, result.EndLine);
        Assert.Contains("namespace TestNamespace", result.Content);
    }

    [Fact]
    public async Task ReadSymbolBodyAsync_ReturnsJsonPayload()
    {
        var json = await _plugin.ReadSymbolBodyAsync(
            _fixture.AddMethodSymbolId,
            contextLines: 0);

        var result = JsonSerializer.Deserialize<SymbolBodyView>(json, JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(_fixture.AddMethodSymbolId, result!.SymbolId);
        Assert.Contains("public int Add", result.Content);
    }

    [Fact]
    public void GetFileOutline_ReturnsJsonPayload()
    {
        var json = _plugin.GetFileOutline(_fixture.RelativeSourcePath);

        var outline = JsonSerializer.Deserialize<FileOutlineView>(json, JsonOptions);
        Assert.NotNull(outline);
        Assert.Single(outline!.Namespaces);
        Assert.Single(outline.Namespaces[0].Types);
        Assert.Single(outline.Namespaces[0].Types[0].Members);
    }

    [Fact]
    public void ListFiles_ReturnsJsonPayload()
    {
        var json = _plugin.ListFiles(
            path: ".",
            filter: ".cs",
            recursive: true,
            maxCount: 10);

        var list = JsonSerializer.Deserialize<FileListResult>(json, JsonOptions);
        Assert.NotNull(list);
        Assert.True(list!.TotalCount >= 1);
        Assert.Contains(list.Files, f => f.FilePath == _fixture.SourceFilePath);
    }

    [Fact]
    public void ReadFile_ReturnsErrorJson_WhenFileMissing()
    {
        var json = _plugin.ReadFile("non-existing-file.cs");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("File not found", errorProp.GetString());
    }
}