using Microsoft.CodeAnalysis;

namespace LLMClient.ContextEngineering.Analysis;

/// <summary>
/// 跨 Service 共享的解决方案状态。
/// 由 ProjectAwarenessService.LoadSolutionAsync 写入，所有 Analysis Service 只读消费。
/// </summary>
public sealed class SolutionContext
{
    public bool IsLoaded => Analyzer.IsLoaded;

    public SymbolIndexService SymbolIndex => Analyzer.IndexService;

    internal RoslynProjectAnalyzer Analyzer { get; }

    public SolutionContext(RoslynProjectAnalyzer analyzer)
    {
        Analyzer = analyzer;
    }

    public async Task LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        if (solutionPath == Analyzer.SolutionDir)
        {
            return;
        }

        Analyzer.CloseCurrentSolution();
        await Analyzer.LoadSolutionAsync(solutionPath, ct);
    }

    public void Clear()
    {
        Analyzer.CloseCurrentSolution();
    }


    internal void SetForTesting(SolutionInfo info)
    {
        Analyzer.SetForTesting(info);
    }

    public SolutionInfo RequireSolutionInfoOrThrow()
        => Analyzer.SolutionInfo ?? throw new InvalidOperationException(
            "No solution loaded. Call LoadSolutionAsync first.");

    public Solution RequireRoslynSolutionOrThrow()
        => Analyzer.CurrentRawSolution ?? throw new InvalidOperationException(
            "No live Roslyn solution. Call LoadSolutionAsync first.");

    public string RequireSolutionDirOrThrow()
    {
        return Analyzer.SolutionDir ??
               throw new InvalidOperationException("Cannot determine solution directory from solution path.");
    }

    public string ToSolutionRelative(string absolutePath)
    {
        var solutionDir = RequireSolutionDirOrThrow();
        return Path.GetRelativePath(solutionDir, absolutePath);
    }

    /// <summary>
    /// 将 Plugin 传入的相对路径（或 "." / 空串）解析为绝对路径。
    /// 绝对路径直接返回，相对路径以 solution 根目录为 base。
    /// </summary>
    public string ResolveToAbsolute(string input)
    {
        var baseDir = RequireSolutionDirOrThrow();
        if (string.IsNullOrWhiteSpace(input) || input is ".")
            return baseDir;

        if (Path.IsPathRooted(input))
            return input;
        return Path.GetFullPath(Path.Combine(baseDir, input));
    }

    /// <summary>
    /// 解析文件过滤器字符串为扩展名列表
    /// </summary>
    public static List<string> ParseFileExtensions(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new List<string>();

        return filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext : "." + ext)
            .ToList();
    }

    /// <summary>
    /// 检查符号是否包含指定位置
    /// </summary>
    public static bool SymbolContainsLocation(SymbolInfo symbol, string filePath, int lineNumber)
    {
        return symbol.Locations.Any(loc =>
            string.Equals(loc.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
            lineNumber >= loc.Location.Start.Line &&
            lineNumber <= loc.Location.End.Line);
    }
}