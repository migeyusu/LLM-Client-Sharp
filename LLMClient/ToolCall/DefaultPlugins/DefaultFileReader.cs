namespace LLMClient.ToolCall.DefaultPlugins;

/// <summary>
/// Default file reader using direct .NET file APIs with automatic encoding detection.
/// Detects BOM, validates UTF-8, and falls back to system default encoding (e.g., GBK).
/// </summary>
public sealed class DefaultFileReader : IFileReader
{
    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var (content, _) = await FileEncodingHelper.ReadTextWithDetectionAsync(path, cancellationToken);
        return content;
    }

    public async Task<string[]> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        var (lines, _) = await FileEncodingHelper.ReadLinesWithDetectionAsync(path, cancellationToken);
        return lines;
    }
}