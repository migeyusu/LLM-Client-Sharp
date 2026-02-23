using System.Text.Json;
using LLMClient.ToolCall.DefaultPlugins;
using ModelContextProtocol.Client;
using Xunit.Abstractions;

// Mark the class to use our fixture. xUnit will create one instance of the fixture
// and pass it to the constructor of our test class.
namespace LLMClient.Test;

public class FileSystemPluginComparisonTests : IClassFixture<FileSystemTestFixture>
{
    private readonly FileSystemPlugin _csPlugin;
    private readonly McpClient _mcpClient;
    private readonly FileSystemTestFixture _fixture;

    private readonly ITestOutputHelper _output;

    // The constructor receives the injected MCP client and the test fixture
    public FileSystemPluginComparisonTests(FileSystemTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _mcpClient = fixture.McpClient; // This should be provided by your DI container in a real test setup

        // IMPORTANT: Both the C# plugin and the TS MCP server must be configured
        // to use the same temporary directory as their allowed path.
        _csPlugin = new FileSystemPlugin();
        _csPlugin.AddAllowedPath(_fixture.TestDirectory);
    }

    [Fact]
    public async Task ReadFile_ShouldReturnIdenticalContent()
    {
        var fullPath = _fixture.ReadmeFilePath;
        // Act
        var csResult = await _csPlugin.ReadFileAsync(fullPath);
        var mcpResult =
            await _mcpClient.CallToolAsync("read_file", new Dictionary<string, object?>() { ["path"] = fullPath });

        // Assert
        Assert.Equal(NormalizeOutput(csResult), NormalizeOutput(mcpResult.GetTextContent()));
    }

    [Fact]
    public async Task WriteFile_ShouldCreateIdenticalFiles()
    {
        // Arrange
        var csFilePath = Path.Combine(_fixture.TestDirectory, "cs_write_test.txt");
        var mcpFilePath = Path.Combine(_fixture.TestDirectory, "mcp_write_test.txt");
        var content = "Content written at " + DateTime.UtcNow;

        // Act
        await _csPlugin.WriteFileAsync(csFilePath, content);
        await _mcpClient.CallToolAsync("write_file",
            new Dictionary<string, object?>() { ["path"] = mcpFilePath, ["content"] = content });

        // Assert
        // The most reliable assertion is to check if the resulting files on disk are identical.
        var csFileContent = await File.ReadAllTextAsync(csFilePath);
        var mcpFileContent = await File.ReadAllTextAsync(mcpFilePath);
        Assert.Equal(csFileContent, mcpFileContent);
    }

    [Fact]
    public async Task ListDirectory_ShouldContainSameEntries()
    {
        var testDirectory = _fixture.TestDirectory;
        // Act
        var csResult = await _csPlugin.ListDirectoryAsync(testDirectory);
        var mcpResult = await _mcpClient.CallToolAsync("list_directory",
            new Dictionary<string, object?>() { ["path"] = testDirectory });

        // Assert
        // We sort the lines because file system entry order is not guaranteed to be the same.
        var csEntries = SortLines(NormalizeOutput(csResult));
        var mcpEntries = SortLines(NormalizeOutput(mcpResult.GetTextContent()));

        Assert.Equal(csEntries, mcpEntries);
    }

    [Fact]
    public async Task EditFile_ShouldProduceIdenticalResults()
    {
        // Arrange
        // Create two identical copies of the source file to edit
        var csEditPath = Path.Combine(_fixture.TestDirectory, "cs_edit_me.txt");
        var mcpEditPath = Path.Combine(_fixture.TestDirectory, "mcp_edit_me.txt");
        File.Copy(_fixture.ReadmeFilePath, csEditPath);
        File.Copy(_fixture.ReadmeFilePath, mcpEditPath);

        var edits = new List<FileSystemPlugin.EditOperation>
        {
            new() { OldText = "multiple lines", NewText = "many, many lines" }
        };

        // Act
        await _csPlugin.EditFileAsync(csEditPath, edits);
        await _mcpClient.CallToolAsync("edit_file",
            new Dictionary<string, object?>() { ["path"] = mcpEditPath, ["edits"] = edits });

        // Assert
        // Again, compare the final state of the files on disk.
        var csFileContent = await File.ReadAllTextAsync(csEditPath);
        var mcpFileContent = await File.ReadAllTextAsync(mcpEditPath);
        Assert.Equal(NormalizeOutput(csFileContent), NormalizeOutput(mcpFileContent));
    }

    [Fact]
    public async Task GetDirectoryTree_ShouldProduceEquivalentJsonStructure()
    {
        var testDirectory = _fixture.TestDirectory;
        // Act
        var csResultJson = await _csPlugin.GetDirectoryTreeAsync(testDirectory);
        var mcpResult = await _mcpClient.CallToolAsync("directory_tree",
            new Dictionary<string, object?>() { ["path"] = testDirectory });
        var mcpResultJson = mcpResult.GetTextContent();

        // Assert
        // Simple string comparison is fragile. We deserialize and compare the object model.
        using var csDoc = JsonDocument.Parse(csResultJson);
        using var mcpDoc = JsonDocument.Parse(mcpResultJson);

        // This is a basic structural check. A full deep-compare would be more robust.
        // For this example, we'll check the root element's properties and child count.
        var csRoot = csDoc.RootElement;
        // The TS version returns an array of entries at the root, while the C# returns a single root object.
        // We need to adjust the assertion to match this structural difference.
        // Let's assume the TS version returns an array for the root, and we want to check its contents.
        var mcpRootArray = mcpDoc.RootElement;

        Assert.Equal(JsonValueKind.Array, mcpRootArray.ValueKind);

        var csRootChildren = csRoot.EnumerateArray().ToList();
        Assert.Equal(mcpRootArray.GetArrayLength(), csRootChildren.Count);

        var mcpReadme = mcpRootArray.EnumerateArray().First(e => e.GetProperty("name").GetString() == "readme.txt");
        Assert.Equal("file", mcpReadme.GetProperty("type").GetString());

        var mcpSubfolder = mcpRootArray.EnumerateArray().First(e => e.GetProperty("name").GetString() == "subfolder");
        Assert.Equal("directory", mcpSubfolder.GetProperty("type").GetString());
        Assert.True(mcpSubfolder.TryGetProperty("children", out var mcpChildren));
        Assert.Equal(1, mcpChildren.GetArrayLength());
    }

    [Fact]
    public async Task ReadMultipleFiles_ShouldReturnAggregatedContent()
    {
        // Arrange
        var pathsToRead = new List<string>
        {
            "readme.txt",
            Path.Combine("subfolder", "data.json"), // Use platform-neutral path combination
            "non_existent_file.txt" // Include a file that doesn't exist to test error handling
        };

        var fullPathsToRead = pathsToRead.Select(p => Path.Combine(_fixture.TestDirectory, p)).ToList();

        // Act
        var csResult = await _csPlugin.ReadMultipleFilesAsync(fullPathsToRead);
        var mcpResult = await _mcpClient.CallToolAsync("read_multiple_files",
            new Dictionary<string, object?>() { ["paths"] = fullPathsToRead });

        // Assert
        // Normalization is key here as paths and error messages might differ slightly.
        var csNormalized = NormalizeOutput(csResult);
        var mcpNormalized = NormalizeOutput(mcpResult.GetTextContent());

        // Split into sections and sort to handle potential ordering differences.
        var csSections = csNormalized.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToList();
        var mcpSections = mcpNormalized.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToList();
        csSections.Sort(StringComparer.Ordinal);
        mcpSections.Sort(StringComparer.Ordinal);

        Assert.Equal(csSections.Count, mcpSections.Count);
        for (int i = 0; i < csSections.Count; i++)
        {
            // For error messages, we just check that both report an error, not the exact text.
            if (csSections[i].Contains("Error") || mcpSections[i].Contains("Error"))
            {
                Assert.Contains("non_existent_file.txt: Error", csSections[i], StringComparison.Ordinal);
                Assert.Contains("non_existent_file.txt: Error", mcpSections[i], StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(csSections[i], mcpSections[i]);
            }
        }
    }

    [Fact]
    public async Task CreateDirectory_ShouldResultInNewDirectory()
    {
        // Arrange
        var csNewDirName = "cs_new_dir";
        var mcpNewDirName = "mcp_new_dir";
        var testDirectory = _fixture.TestDirectory;
        var csNewDirPath = Path.Combine(testDirectory, csNewDirName);
        var mcpNewDirPath = Path.Combine(testDirectory, mcpNewDirName);

        // Act
        await _csPlugin.CreateDirectoryAsync(csNewDirPath);
        var result = await _mcpClient.CallToolAsync("create_directory",
            new Dictionary<string, object?>() { ["path"] = mcpNewDirPath });
        _output.WriteLine(result.GetTextContent());

        // Assert
        // The proof is whether the directories exist on the file system.
        Assert.True(Directory.Exists(csNewDirPath), "C# plugin should have created the directory.");
        Assert.True(Directory.Exists(mcpNewDirPath), "MCP server should have created the directory.");
    }

    [Fact]
    public async Task ListDirectoryWithSizes_ShouldContainSimilarContent()
    {
        // Arrange
        var testDirectory = _fixture.TestDirectory;

        // Act
        // Act for both C# plugin and MCP client
        var csResult = await _csPlugin.ListDirectoryWithSizesAsync(testDirectory, "name");
        var mcpResult =
            await _mcpClient.CallToolAsync("list_directory_with_sizes",
                new Dictionary<string, object?>() { ["path"] = testDirectory, ["sortBy"] = "name" });
        _output.WriteLine(mcpResult.GetTextContent());
        // Assert
        // The output contains file sizes which are deterministic. We can sort and compare.
        // However, the summary part might have different wording. Let's split them.
        var csLines = NormalizeOutput(csResult).Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var mcpLines = NormalizeOutput(mcpResult.GetTextContent()).Split('\n').Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // Extract entries and summaries
        var csEntries = csLines.Where(l => l.StartsWith("[DIR]") || l.StartsWith("[FILE]")).ToList();
        var mcpEntries = mcpLines.Where(l => l.StartsWith("[DIR]") || l.StartsWith("[FILE]")).ToList();
        var csSummary = csLines.Where(l => l.StartsWith("Total") || l.StartsWith("Combined")).ToList();
        var mcpSummary = mcpLines.Where(l => l.StartsWith("Total") || l.StartsWith("Combined")).ToList();

        csEntries.Sort(StringComparer.Ordinal);
        mcpEntries.Sort(StringComparer.Ordinal);

        // Compare entries (ignoring whitespace padding differences)
        Assert.Equal(csEntries.Count, mcpEntries.Count);
        for (int i = 0; i < csEntries.Count; i++)
        {
            var csCleaned = string.Join(" ", csEntries[i].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var mcpCleaned = string.Join(" ", mcpEntries[i].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            Assert.Equal(csCleaned, mcpCleaned);
        }

        // Compare summaries (just check for presence of keywords)
        Assert.Contains("Total", string.Join(" ", csSummary));
        Assert.Contains("Combined size", string.Join(" ", csSummary));
        Assert.Contains("Total", string.Join(" ", mcpSummary));
        Assert.Contains("Combined size", string.Join(" ", mcpSummary));
    }

    [Fact]
    public async Task MoveFile_ShouldRelocateFile()
    {
        // Arrange
        var csSourcePath = Path.Combine(_fixture.TestDirectory, "cs_move_source.txt");
        var csDestPath = Path.Combine(_fixture.SubFolderPath, "cs_move_dest.txt");
        var mcpDestPath = Path.Combine(_fixture.SubFolderPath, "mcp_move_dest.txt");
        var mcpSourcePath = Path.Combine(_fixture.TestDirectory, "mcp_move_source.txt");

        await File.WriteAllTextAsync(csSourcePath, "move me");
        await File.WriteAllTextAsync(mcpSourcePath, "move me");

        // Act
        await _csPlugin.MoveFileAsync(csSourcePath, csDestPath);
        await _mcpClient.CallToolAsync("move_file",
            new Dictionary<string, object?>() { ["source"] = mcpSourcePath, ["destination"] = mcpDestPath });

        // Assert
        Assert.False(File.Exists(csSourcePath), "C# source file should no longer exist.");
        Assert.True(File.Exists(csDestPath), "C# destination file should exist.");
        Assert.False(File.Exists(mcpSourcePath), "MCP source file should no longer exist.");
        Assert.True(File.Exists(Path.Combine(_fixture.TestDirectory, mcpDestPath)),
            "MCP destination file should exist.");

        Assert.Equal("move me", await File.ReadAllTextAsync(csDestPath));
    }

    [Fact]
    public async Task SearchFiles_ShouldFindMatchingEntries()
    {
        // Arrange
        var searchPattern = "data";
        var excludePattern = new List<string> { "**/ignore/**" }; // Add a pattern if needed

        // Act
        var csResult = await _csPlugin.SearchFilesAsync(_fixture.TestDirectory, searchPattern, excludePattern);
        var mcpResult = await _mcpClient.CallToolAsync("search_files",
            new Dictionary<string, object?>()
            {
                ["path"] = _fixture.TestDirectory, ["pattern"] = searchPattern, ["excludePatterns"] = excludePattern
            });

        // Assert
        var csPaths = SortLines(NormalizeOutput(csResult));
        var mcpPaths = SortLines(NormalizeOutput(mcpResult.GetTextContent()));

        // Since the C# version returns full paths and TS might return relative, we need to make them comparable.
        // Let's assume both return paths relative to the test directory after normalization.
        var mcpFullPaths = string.Join('\n',
            mcpPaths.Split('\n').Select(p => NormalizeOutput(Path.Combine(_fixture.TestDirectory, p))));

        Assert.Equal(csPaths, mcpFullPaths);
        Assert.Contains("data.json", csPaths, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFileInfo_ShouldReturnSimilarMetadata()
    {
        // Arrange
        var fullPath = _fixture.ReadmeFilePath;

        // Act
        var csResult = await _csPlugin.GetFileInfoAsync(fullPath);
        var mcpResult = await _mcpClient.CallToolAsync("get_file_info",
            new Dictionary<string, object?>() { ["path"] = fullPath });

        // Assert
        var csInfo = ParseInfoToDictionary(csResult);
        var mcpInfo = ParseInfoToDictionary(mcpResult.GetTextContent());

        // Compare deterministic properties
        Assert.Equal("File", csInfo["Type"]);
        Assert.Equal("true", mcpInfo["isFile"], ignoreCase: true);
        Assert.Equal("false", mcpInfo["isDirectory"], ignoreCase: true);

        Assert.Equal(csInfo["Name"], mcpInfo["name"]);
        Assert.Equal(csInfo["Size"], mcpInfo["size"]); // Compare raw size string before formatting

        // Timestamps can have minor differences in precision, so a fuzzy match is better.
        // For this example, we'll assume they are close enough if the test runs quickly.
        var csModified = DateTime.Parse(csInfo["LastModified"]);
        var mcpModified = DateTime.Parse(mcpInfo["modified"]);
        Assert.True((csModified - mcpModified).TotalSeconds < 2, "Modification times should be very close.");
    }

    /*[Fact]
    public async Task ListAllowedDirectories_ShouldReturnConfiguredPath()
    {
        // Arrange & Act
        var csResult = _csPlugin.ListAllowedDirectories();
        var mcpResult = await _mcpClient.CallToolAsync("list_allowed_directories");

        // Assert
        var csNormalized = NormalizeOutput(csResult);
        var mcpNormalized = NormalizeOutput(mcpResult.GetTextContent());

        // Both should contain the single temporary test directory path
        var expectedPath = NormalizeOutput(_fixture.TestDirectory);
        Assert.Contains(expectedPath, csNormalized, StringComparison.Ordinal);
        Assert.Contains(expectedPath, mcpNormalized, StringComparison.Ordinal);
    }*/


    #region Helper Methods for GetFileInfo test

    /// <summary>
    /// A simple parser to convert the key-value string output of GetFileInfo into a dictionary.
    /// </summary>
    private Dictionary<string, string> ParseInfoToDictionary(string infoText)
    {
        return infoText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }

    #endregion


    #region Helper Methods for Normalization

    /// <summary>
    /// Normalizes string output for reliable comparison by handling line endings and path separators.
    /// </summary>
    private string NormalizeOutput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text.Replace("\r\n", "\n") // Unify line endings to LF
            .Replace('\\', '/') // Unify path separators to /
            .Trim();
    }

    /// <summary>
    /// Sorts the lines of a string alphabetically. Useful for comparing directory listings.
    /// </summary>
    private static string SortLines(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(lines, StringComparer.Ordinal);
        return string.Join('\n', lines);
    }

    #endregion
}