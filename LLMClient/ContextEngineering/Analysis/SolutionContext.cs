using Microsoft.CodeAnalysis;

namespace LLMClient.ContextEngineering.Analysis;

/// <summary>
/// 跨 Service 共享的解决方案状态。
/// 由 ProjectAwarenessService.LoadSolutionAsync 写入，所有 Analysis Service 只读消费。
/// </summary>
internal sealed class SolutionContext
{
    private volatile SolutionInfo? _solutionInfo;
    private volatile Solution? _roslynSolution;

    public SolutionInfo? SolutionInfo => _solutionInfo;

    public Solution? RoslynSolution => _roslynSolution;

    public bool IsLoaded => _solutionInfo != null && _roslynSolution != null;

    public RoslynProjectAnalyzer Analyzer { get; }

    public string? SolutionDir { get; private set; }

    public SolutionContext(RoslynProjectAnalyzer analyzer)
    {
        Analyzer = analyzer;
    }

    public async Task LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        Analyzer.CloseCurrentSolution();
        var info = await Analyzer.AnalyzeSolutionAsync(solutionPath, ct);
        _solutionInfo = info;
        SolutionDir = Path.GetDirectoryName(solutionPath)
                      ?? throw new ArgumentException("Invalid solution path");
        _roslynSolution = Analyzer.CurrentRawSolution;
    }

    internal void Clear()
    {
        _solutionInfo = null;
        _roslynSolution = null;
    }


    internal void SetForTesting(SolutionInfo info)
    {
        _solutionInfo = info;
        _roslynSolution = null; // 让依赖 Roslyn 的分支走 catch/fallback
        SolutionDir = Path.GetDirectoryName(info.SolutionPath);
    }


    public SolutionInfo RequireSolutionInfoOrThrow()
        => _solutionInfo ?? throw new InvalidOperationException(
            "No solution loaded. Call LoadSolutionAsync first.");

    public Solution RequireRoslynSolutionOrThrow()
        => _roslynSolution ?? throw new InvalidOperationException(
            "No live Roslyn solution. Call LoadSolutionAsync first.");

    public string RequireSolutionDirOrThrow()
    {
        return SolutionDir ??
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