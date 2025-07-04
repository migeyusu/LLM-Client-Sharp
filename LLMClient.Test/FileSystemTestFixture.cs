// This fixture creates a temporary directory with some files and folders for our tests.
// It ensures a clean, predictable environment and cleans up after itself.

using System.Diagnostics;
using ModelContextProtocol.Client;

namespace LLMClient.Test;

public class FileSystemTestFixture : IDisposable
{
    public string TestDirectory { get; }
    public string ReadmeFilePath { get; }
    public string SubFolderPath { get; }
    public string DataJsonPath { get; }

    public IMcpClient McpClient { get; }

    public FileSystemTestFixture()
    {
        // Create a unique temporary directory for the test run
        TestDirectory = Path.Combine(Path.GetTempPath(), "FileSystemPluginTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestDirectory);
        Debug.WriteLine($"Test Directory created:{TestDirectory}");
        McpClient = McpClientFactory.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions()
        {
            Name = "filesystem",
            Command = "npx",
            Arguments = new string[] { "-y", "@modelcontextprotocol/server-filesystem", TestDirectory }
        })).GetAwaiter().GetResult();
        var mcpClientTools = McpClient.ListToolsAsync().GetAwaiter().GetResult();
        Debug.WriteLine("Create mcp tools successfully");
        foreach (var mcpClientTool in mcpClientTools)
        {
            Debug.Write(mcpClientTool.ToString());
        }

        // --- Seed the directory with test data ---
        ReadmeFilePath = Path.Combine(TestDirectory, "readme.txt");
        File.WriteAllText(ReadmeFilePath,
            "This is a test file for the FileSystemPlugin.\nIt has multiple lines.\nEnd of file.");

        SubFolderPath = Path.Combine(TestDirectory, "subfolder");
        Directory.CreateDirectory(SubFolderPath);

        DataJsonPath = Path.Combine(SubFolderPath, "data.json");
        File.WriteAllText(DataJsonPath, @"{ ""key"": ""value"" }");
    }

    // This method is called after all tests in the class have run
    public void Dispose()
    {
        // Cleanup: Recursively delete the temporary directory
        if (Directory.Exists(TestDirectory))
        {
            Directory.Delete(TestDirectory, recursive: true);
        }
    }
}