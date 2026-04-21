namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// Abstraction layer for file read operations.
/// Allows switching between direct .NET file reading and encrypted/PowerShell-based reading.
/// </summary>
public interface IFileReader
{
    /// <summary>
    /// Asynchronously reads the entire contents of a file as a single string.
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously reads all lines of a file.
    /// </summary>
    Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);
}