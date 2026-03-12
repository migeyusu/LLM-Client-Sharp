// File: LLMClient.Test/Agent/Tools/CodeSearchPluginTests.cs

using System.Text.Json;
using LLMClient.ContextEngineering.Tools;
using LLMClient.ContextEngineering.Tools.Models;
using Moq;
using Xunit;

namespace LLMClient.Test.Agent.Tools;

public sealed class CodeSearchPluginTests : IClassFixture<CodeSearchTestFixture>
{
    private readonly CodeSearchTestFixture _fixture;
    private readonly CodeSearchPlugin _plugin;

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
        var result = JsonSerializer.Deserialize<TextSearchView>(json);
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
        var result = JsonSerializer.Deserialize<TextSearchView>(json);
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
        var result = JsonSerializer.Deserialize<TextSearchView>(json);
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
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json);
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
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json);
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
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json);
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
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json);
        Assert.NotNull(result);
        Assert.Equal(attributeName, result.AttributeName);
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

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json);
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
    }

    [Fact]
    public void FindByAttribute_WithNonExistingAttribute_ReturnsEmptyResult()
    {
        // Arrange
        var attributeName = "NonExistentAttr";

        // Act
        var json = _plugin.FindByAttribute(attributeName);

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<AttributeSearchView>(json);
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

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<TextSearchView>(json);
        Assert.NotNull(result);
        Assert.Equal(pattern, result.Query);
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

        // Assert
        Assert.NotNull(json);
        var result = JsonSerializer.Deserialize<TextSearchView>(json);
        Assert.NotNull(result);
        Assert.Equal("Regex", result.SearchMode);
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
    public async Task SearchSemanticAsync_WithException_ReturnsErrorJson()
    {
        // Arrange
        _fixture.MockEmbeddingService
            .Setup(s => s.SearchByEmbeddingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        var query = "test query";

        // Act
        var json = await _plugin.SearchSemanticAsync(query);

        // Assert
        Assert.NotNull(json);
        // 应该 fallback 到文本搜索，不返回错误
        var result = JsonSerializer.Deserialize<SemanticSearchView>(json);
        Assert.NotNull(result);
        Assert.Equal("Fallback", result.Source);
    }

    // ── Integration Tests ─────────────────────────────────────────────

    [Fact]
    public async Task Plugin_FullWorkflow_SearchAndFindSimilar()
    {
        // Step 1: 文本搜索找到感兴趣的代码
        var searchJson = _plugin.SearchText("HttpClient");
        var searchResult = JsonSerializer.Deserialize<TextSearchView>(searchJson);
        Assert.NotNull(searchResult);
        Assert.True(searchResult.TotalMatches > 0);

        // Step 2: 提取代码片段进行相似代码搜索
        var snippet = searchResult.Results.First().LineContent;
        var similarJson = await _plugin.FindSimilarCodeAsync(snippet);
        var similarResult = JsonSerializer.Deserialize<SemanticSearchView>(similarJson);
        Assert.NotNull(similarResult);
    }

    [Fact]
    public void Plugin_FullWorkflow_AttributeSearch()
    {
        // Step 1: 查找所有 Obsolete 成员
        var attrJson = _plugin.FindByAttribute("Obsolete");
        var attrResult = JsonSerializer.Deserialize<AttributeSearchView>(attrJson);
        Assert.NotNull(attrResult);
        Assert.True(attrResult.TotalCount > 0);

        // Step 2: 对找到的文件进行详细搜索
        var firstResult = attrResult.Results.First();
        if (firstResult.Location != null)
        {
            var relPath = _fixture.Context.ToSolutionRelative(firstResult.Location.FilePath);
            var fileJson = _plugin.SearchInFile(relPath, "Obsolete");
            var fileResult = JsonSerializer.Deserialize<TextSearchView>(fileJson);
            Assert.NotNull(fileResult);
            Assert.True(fileResult.TotalMatches > 0);
        }
    }
}