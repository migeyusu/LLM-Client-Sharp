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
        var info = RequireSolutionInfoOrThrow();
        return Path.GetDirectoryName(info.SolutionPath)
               ?? throw new InvalidOperationException("Cannot determine solution directory from solution path.");
    }
}