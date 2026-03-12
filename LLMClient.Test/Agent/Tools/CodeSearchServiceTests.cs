// File: LLMClient.Test/Agent/Tools/CodeSearchServiceTests.cs

using LLMClient.ContextEngineering.Tools;
using LLMClient.ContextEngineering.Tools.Models;
using Moq;
using Xunit;

namespace LLMClient.Test.Agent.Tools;

public sealed class CodeSearchServiceTests : IClassFixture<CodeSearchTestFixture>
{
    private readonly CodeSearchTestFixture _fixture;
    private readonly CodeSearchService _service;

    public CodeSearchServiceTests(CodeSearchTestFixture fixture)
    {
        _fixture = fixture;
        _service = _fixture.SearchService;
    }

    // ── SearchText Tests ──────────────────────────────────────────────

    [Fact]
    public void SearchText_WithExistingPattern_ReturnsMatches()
    {
        // Arrange
        var pattern = "HttpClient";

        // Act
        var result = _service.SearchText(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pattern, result.Query);
        Assert.Equal("Text", result.SearchMode);
        Assert.True(result.TotalMatches >= 2); // UserService.cs and AuthService.cs
        Assert.True(result.Results.Any(r => r.FilePath.Contains("UserService.cs")));
        Assert.True(result.Results.Any(r => r.FilePath.Contains("AuthService.cs")));
    }

    [Fact]
    public void SearchText_WithNonExistingPattern_ReturnsEmpty()
    {
        // Arrange
        var pattern = "NonExistentPattern12345";

        // Act
        var result = _service.SearchText(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalMatches);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void SearchText_WithRegexPattern_ReturnsMatches()
    {
        // Arrange
        var pattern = @"async\s+Task";

        // Act
        var result = _service.SearchText(pattern, useRegex: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Regex", result.SearchMode);
        Assert.True(result.TotalMatches >= 2);
        Assert.All(result.Results, r => Assert.Contains("async Task", r.LineContent));
    }

    [Fact]
    public void SearchText_WithScopeFilter_ReturnsFilteredResults()
    {
        // Arrange
        var pattern = "HttpClient";
        var scope = "TestProject/Services";

        // Act
        var result = _service.SearchText(pattern, scope: scope);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalMatches >= 1);
        Assert.All(result.Results, r =>
            Assert.Contains("Services", r.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchText_WithFileFilter_ReturnsOnlyMatchingExtensions()
    {
        // Arrange
        var pattern = "api";
        var fileFilter = ".cs";

        // Act
        var result = _service.SearchText(pattern, fileFilter: fileFilter);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Results, r => Assert.EndsWith(".cs", r.FilePath));
    }

    [Fact]
    public void SearchText_WithContextLines_ReturnsContext()
    {
        // Arrange
        var pattern = "GetUserAsync";
        var contextLines = 2;

        // Act
        var result = _service.SearchText(pattern, contextLines: contextLines);

        // Assert
        Assert.NotNull(result);
        var match = result.Results.FirstOrDefault(r => r.LineContent.Contains("GetUserAsync"));
        Assert.NotNull(match);
        Assert.NotNull(match.ContextBefore);
        Assert.NotNull(match.ContextAfter);
    }

    // ── SearchSemanticAsync Tests ─────────────────────────────────────

    [Fact]
    public async Task SearchSemanticAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var query = "authentication logic";

        // Act
        var result = await _service.SearchSemanticAsync(query, topK: 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.True(result.TotalResults > 0);
        Assert.Equal("RAG", result.Source);
        Assert.Contains(result.Results, r => r.FilePath.Contains("AuthService.cs"));
    }

    [Fact]
    public async Task SearchSemanticAsync_WithHttpClientQuery_ReturnsRelevantCode()
    {
        // Arrange
        var query = "HTTP client usage";

        // Act
        var result = await _service.SearchSemanticAsync(query, topK: 5);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalResults > 0);
        Assert.Contains(result.Results, r =>
            r.CodeSnippet.Contains("HttpClient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchSemanticAsync_WhenEmbeddingFails_FallsBackToTextSearch()
    {
        // Arrange
        var query = "TODO";
        _fixture.MockEmbeddingService
            .Setup(s => s.SearchByEmbeddingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Embedding service unavailable"));

        // Act
        var result = await _service.SearchSemanticAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback", result.Source);
        Assert.Contains(result.Results, r => r.CodeSnippet.Contains("TODO"));
    }

    [Fact]
    public async Task SearchSemanticAsync_RespectsTopKLimit()
    {
        // Arrange
        var query = "async method";
        var topK = 1;

        // Act
        var result = await _service.SearchSemanticAsync(query, topK: topK);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Results.Count <= topK);
    }

    // ── FindSimilarCodeAsync Tests ────────────────────────────────────

    [Fact]
    public async Task FindSimilarCodeAsync_WithCodeSnippet_ReturnsMatches()
    {
        // Arrange
        var snippet = "var response = await _httpClient.GetAsync(url);";

        // Act
        var result = await _service.FindSimilarCodeAsync(snippet, topK: 5);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalResults > 0);
        Assert.Contains(result.Results, r =>
            r.CodeSnippet.Contains("HttpClient", StringComparison.OrdinalIgnoreCase));
    }

    // ── FindByAttribute Tests ─────────────────────────────────────────

    [Fact]
    public void FindByAttribute_WithObsoleteAttribute_ReturnsMarkedSymbols()
    {
        // Arrange
        var attributeName = "Obsolete";

        // Act
        var result = _service.FindByAttribute(attributeName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(attributeName, result.AttributeName);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Results, r =>
            r.Name == "LegacyMethod" && r.Attributes.Contains("Obsolete"));
    }

    [Fact]
    public void FindByAttribute_WithAttributeSuffix_StillMatches()
    {
        // Arrange
        var attributeName = "ObsoleteAttribute";

        // Act
        var result = _service.FindByAttribute(attributeName);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Results, r => r.Attributes.Contains("Obsolete"));
    }

    [Fact]
    public void FindByAttribute_WithNonExistingAttribute_ReturnsEmpty()
    {
        // Arrange
        var attributeName = "NonExistentAttribute";

        // Act
        var result = _service.FindByAttribute(attributeName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void FindByAttribute_WithScopeFilter_ReturnsFilteredResults()
    {
        // Arrange
        var attributeName = "Obsolete";
        var scope = "TestProject";

        // Act
        var result = _service.FindByAttribute(attributeName, scope: scope);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.All(result.Results, r =>
            Assert.Contains("TestProject", r.Location?.FilePath ?? string.Empty));
    }

    // ── SearchInFile Tests ────────────────────────────────────────────

    [Fact]
    public void SearchInFile_WithValidPath_ReturnsMatches()
    {
        // Arrange
        var filePath = "TestProject/Services/UserService.cs";
        var pattern = "async";

        // Act
        var result = _service.SearchInFile(filePath, pattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.FilesSearched);
        Assert.True(result.TotalMatches >= 1);
        Assert.All(result.Results, r =>
        {
            Assert.EndsWith("UserService.cs", r.FilePath);
            Assert.Contains("async", r.LineContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SearchInFile_WithRegex_ReturnsMatches()
    {
        // Arrange
        var filePath = "TestProject/Services/UserService.cs";
        var pattern = @"public\s+\w+\s+\w+\(";

        // Act
        var result = _service.SearchInFile(filePath, pattern, useRegex: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalMatches >= 2); // Multiple public methods
    }

    [Fact]
    public void SearchInFile_WithNonExistingFile_ThrowsException()
    {
        // Arrange
        var filePath = "NonExisting/File.cs";
        var pattern = "test";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            _service.SearchInFile(filePath, pattern));
    }

    [Fact]
    public void SearchInFile_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var filePath = "TestProject/Config.json";
        var pattern = "NonExistentPattern12345";

        // Act
        var result = _service.SearchInFile(filePath, pattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalMatches);
        Assert.Empty(result.Results);
    }

    // ── Edge Cases ────────────────────────────────────────────────────

    [Fact]
    public void SearchText_WithEmptyPattern_ReturnsEmpty()
    {
        // Arrange
        var pattern = string.Empty;

        // Act
        var result = _service.SearchText(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalMatches);
    }

    [Fact]
    public async Task SearchSemanticAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        var query = string.Empty;

        // Act
        var result = await _service.SearchSemanticAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalResults);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(100)]
    public async Task SearchSemanticAsync_ClampsTopKToValidRange(int topK)
    {
        // Arrange
        var query = "test";

        // Act
        var result = await _service.SearchSemanticAsync(query, topK);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.Results.Count, 0, 50); // MaxSemanticResults = 50
    }
}