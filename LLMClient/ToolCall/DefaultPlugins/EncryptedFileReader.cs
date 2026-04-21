using LLMClient.Project;

namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// Encrypted file reader that uses PowerShell to read files.
/// Wraps <see cref="DotNetFileProcessor.SafeReadyByPs"/> for reading.
/// </summary>
public sealed class EncryptedFileReader : IFileReader
{
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() => DotNetFileProcessor.SafeReadyByPs(path), cancellationToken);

    public Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
        => Task.Run(() => DotNetFileProcessor.SafeReadyByPs(path).Split('\n'), cancellationToken);
}