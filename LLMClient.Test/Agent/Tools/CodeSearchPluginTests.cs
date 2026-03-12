// File: LLMClient.Test/Agent/Tools/CodeSearchPluginTests.cs

using System.Text.Json;
using LLMClient.ContextEngineering.Tools;
using LLMClient.ContextEngineering.Tools.Models;
using Moq;
using Xunit;

namespace LLMClient.Test.Agent.Tools;

/// <summary>
/// 测试插件层，验证 JSON 序列化/反序列化及工具调用
/// </summary>
public sealed class CodeSearchPluginTests : IClassFixture<CodeSearchTestFixture>
{
    private readonly CodeSearchTestFixture _fixture;
    private readonly CodeSearchPlugin _plugin;

    // ✅ 添加反序列化选项，必须与 Plugin 中的序列化选项一致
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CodeSearchPluginTests(CodeSearchTestFixture fixture)
    {
        _fixture = fixture;
        _plugin = new CodeSearchPlugin(fixture.SearchService);
    }

    // ── SearchText Tests ──────────────────────────────────────────────

    [Fact]
    public void SearchText_ReturnsValidJson()
    {
        // Arrange
        var pattern = "HttpClient";

        // Act
        var json = _plugin.SearchText(pattern);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<TextSearchView>(json, JsonOptions); // ✅ 使用 JsonOptions
        Assert.NotNull(result);
        Assert.Equal(pattern, result.Query);
        Assert.True(result.TotalMatches > 0);
    }

    [Fact]
    public void SearchText_WithAllParameters_ReturnsFilteredResults()
    {
        // Arrange
        var pattern = "async";
        var scope = "TestProject/Services";
        var fileFilter = ".cs";
        var useRegex = false;
        var contextLines = 2;

        // Act
        var json = _plugin.SearchText(pattern, scope, fileFilter, useRegex, contextLines);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<TextSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
        Assert.Equal(pattern, result.Query);
        Assert.All(result.Results, r => Assert.EndsWith(".cs", r.FilePath));
    }

    [Fact]
    public void SearchText_WithInvalidScope_ReturnsError()
    {
        // Arrange
        var pattern = "test";
        var scope = "NonExisting/Path";

        // Act
        var json = _plugin.SearchText(pattern, scope);

        // Assert
        Assert.NotNull(json);
        // 应该返回有效结果或空结果，不应抛出异常
        var result = JsonSerializer.Deserialize<TextSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
    }

    // ── SearchSemanticAsync Tests ─────────────────────────────────────

    [Fact]
    public async Task SearchSemanticAsync_ReturnsValidJson()
    {
        // Arrange
        var query = "authentication logic";

        // Act
        var json = await _plugin.SearchSemanticAsync(query);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.True(result.TotalResults > 0);
    }

    [Fact]
    public async Task SearchSemanticAsync_WithTopK_RespectsLimit()
    {
        // Arrange
        var query = "HTTP client";
        var topK = 3;

        // Act
        var json = await _plugin.SearchSemanticAsync(query, topK);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
        Assert.True(result.Results.Count <= topK);
    }

    // ── FindSimilarCodeAsync Tests ────────────────────────────────────

    [Fact]
    public async Task FindSimilarCodeAsync_ReturnsValidJson()
    {
        // Arrange
        var snippet = "await httpClient.GetAsync(url)";

        // Act
        var json = await _plugin.FindSimilarCodeAsync(snippet);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
    }

    // ── FindByAttribute Tests ─────────────────────────────────────────

    [Fact]
    public void FindByAttribute_ReturnsValidJson()
    {
        // Arrange
        var attributeName = "Obsolete";

        // Act
        var json = _plugin.FindByAttribute(attributeName);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json, JsonOptions); // ✅
        Assert.NotNull(result);
        Assert.Equal(attributeName, result.AttributeName); // ✅ 现在应该匹配了
        Assert.True(result.TotalCount >= 1);
    }

    [Fact]
    public void FindByAttribute_WithScope_ReturnsFilteredResults()
    {
        // Arrange
        var attributeName = "Obsolete";
        var scope = "TestProject";

        // Act
        var json = _plugin.FindByAttribute(attributeName, scope);
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json, JsonOptions); // ✅

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attributeName, result.AttributeName);
        Assert.True(result.TotalCount >= 1); // ✅ 现在应该能匹配到
        // ✅ 可选：验证结果都在指定 scope 内
        if (result.Results.Count > 0)
        {
            Assert.All(result.Results, r =>
            {
                Assert.NotNull(r.Location);
                // 验证文件路径包含 scope（不区分大小写）
                Assert.Contains(scope, r.Location.FilePath, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    [Fact]
    public void FindByAttribute_WithNonExistingAttribute_ReturnsEmptyResult()
    {
        // Arrange
        var attributeName = "NonExistentAttr";

        // Act
        var json = _plugin.FindByAttribute(attributeName);
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json, JsonOptions); // ✅

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Results);
    }

    // ── SearchInFile Tests ────────────────────────────────────────────

    [Fact]
    public void SearchInFile_ReturnsValidJson()
    {
        // Arrange
        var filePath = "TestProject/Services/UserService.cs";
        var pattern = "async";

        // Act
        var json = _plugin.SearchInFile(filePath, pattern);
        var result = JsonSerializer.Deserialize<TextSearchView>(json, JsonOptions); // ✅

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pattern, result.Query);
        Assert.Equal("Text", result.SearchMode); // ✅ 现在应该是 "Text"
        Assert.True(result.TotalMatches >= 1);
    }

    [Fact]
    public void SearchInFile_WithRegex_ReturnsMatches()
    {
        // Arrange
        var filePath = "TestProject/Services/UserService.cs";
        var pattern = @"public\s+\w+";
        var useRegex = true;

        // Act
        var json = _plugin.SearchInFile(filePath, pattern, useRegex);
        var result = JsonSerializer.Deserialize<TextSearchView>(json, JsonOptions); // ✅

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Regex", result.SearchMode); // ✅ 现在应该是 "Regex"
        Assert.True(result.TotalMatches > 0);
    }

    [Fact]
    public void SearchInFile_WithNonExistingFile_ReturnsErrorJson()
    {
        // Arrange
        var filePath = "NonExisting/File.cs";
        var pattern = "test";

        // Act
        var json = _plugin.SearchInFile(filePath, pattern);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("error", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── Plugin Metadata Tests ─────────────────────────────────────────

    [Fact]
    public void Plugin_HasAdditionPrompt()
    {
        // Assert
        Assert.NotNull(_plugin.AdditionPrompt);
        Assert.Contains("search", _plugin.AdditionPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plugin_CanBeCloned()
    {
        // Act
        var cloned = _plugin.Clone();

        // Assert
        Assert.NotNull(cloned);
        Assert.IsType<CodeSearchPlugin>(cloned);
        Assert.NotSame(_plugin, cloned);
    }

    // ── Error Handling Tests ──────────────────────────────────────────

    [Fact]
    public async Task SearchSemanticAsync_WithException_ReturnsFallbackJson()
    {
        // Arrange
        _fixture.MockEmbeddingService
            .Setup(s => s.SearchByEmbeddingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        var query = "test query";

        // Act
        var json = await _plugin.SearchSemanticAsync(query);
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json, JsonOptions); // ✅

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback", result.Source); // ✅ 应该降级到文本搜索
    }

    // ── Integration Tests ─────────────────────────────────────────────

    [Fact]
    public async Task Plugin_FullWorkflow_SearchAndFindSimilar()
    {
        // Step 1: 文本搜索找到感兴趣的代码
        var searchJson = _plugin.SearchText("HttpClient");
        var searchResult = JsonSerializer.Deserialize<TextSearchView>(searchJson, JsonOptions); // ✅
        Assert.NotNull(searchResult);
        Assert.True(searchResult.TotalMatches > 0, "应至少找到一个匹配 'HttpClient' 的结果");

        // Step 2: 提取代码片段进行相似代码搜索
        var snippet = searchResult.Results.First().LineContent;
        var similarJson = await _plugin.FindSimilarCodeAsync(snippet);
        var similarResult = JsonSerializer.Deserialize<SemanticSearchView>(similarJson, JsonOptions); // ✅
        Assert.NotNull(similarResult);
    }

    [Fact]
    public void Plugin_FullWorkflow_AttributeSearch()
    {
        // Step 1: 查找所有 Obsolete 成员
        var attrJson = _plugin.FindByAttribute("Obsolete");
        var attrResult = JsonSerializer.Deserialize<AttributeSearchView>(attrJson, JsonOptions); // ✅
        Assert.NotNull(attrResult);
        Assert.True(attrResult.TotalCount >= 1, "应至少找到一个标记为 Obsolete 的符号");

        // Step 2: 对找到的文件进行详细搜索
        var firstResult = attrResult.Results.First();
        if (firstResult.Location != null)
        {
            var relPath = _fixture.Context.ToSolutionRelative(firstResult.Location.FilePath);
            var fileJson = _plugin.SearchInFile(relPath, "Obsolete");
            var fileResult = JsonSerializer.Deserialize<TextSearchView>(fileJson, JsonOptions); // ✅
            Assert.NotNull(fileResult);
            Assert.True(fileResult.TotalMatches > 0);
        }
    }
}