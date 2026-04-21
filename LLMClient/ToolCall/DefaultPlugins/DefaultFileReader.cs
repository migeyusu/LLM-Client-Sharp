namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// Default file reader using direct .NET file APIs.
/// </summary>
public sealed class DefaultFileReader : IFileReader
{
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);

    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllLinesAsync(path, cancellationToken);
}