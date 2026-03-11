
using LLMClient.ContextEngineering.Tools.Models;

namespace LLMClient.ContextEngineering.Tools;

public interface ICodeReadingService
{
    ReadFileResult ReadFile(string path, int? startLine = null, int? endLine = null, int? maxTokens = null);

    Task<SymbolBodyView> ReadSymbolBodyAsync(
        string symbolId,
        int contextLines = 0,
        CancellationToken ct = default);

    FileOutlineView GetFileOutline(string path);

    FileListResult ListFiles(
        string path = ".",
        string? filter = null,
        bool recursive = true,
        int maxCount = 300);
}