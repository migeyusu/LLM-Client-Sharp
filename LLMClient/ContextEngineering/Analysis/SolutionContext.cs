using Microsoft.CodeAnalysis;

namespace LLMClient.ContextEngineering.Analysis;

/// <summary>
/// 跨 Service 共享的解决方案状态。
/// 由 ProjectAwarenessService.LoadSolutionAsync 写入，所有 Analysis Service 只读消费。
/// </summary>
public sealed class SolutionContext
{
    private volatile SolutionInfo? _solutionInfo;
    private volatile Solution? _roslynSolution;

    public SolutionInfo? SolutionInfo => _solutionInfo;

    public Solution? RoslynSolution => _roslynSolution;

    public bool IsLoaded => _solutionInfo != null && _roslynSolution != null;

    private readonly RoslynProjectAnalyzer _analyzer;

    public string? SolutionDir { get; private set; }

    public SolutionContext(RoslynProjectAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public async Task LoadSolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        _analyzer.CloseCurrentSolution();
        var info = await _analyzer.AnalyzeSolutionAsync(solutionPath, ct);
        _solutionInfo = info;
        SolutionDir = Path.GetDirectoryName(solutionPath)
                       ?? throw new ArgumentException("Invalid solution path");
        _roslynSolution = _analyzer.CurrentSolution;
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


    public SolutionInfo RequireSolutionInfo()
        => _solutionInfo ?? throw new InvalidOperationException(
            "No solution loaded. Call LoadSolutionAsync first.");

    public Solution RequireRoslynSolution()
        => _roslynSolution ?? throw new InvalidOperationException(
            "No live Roslyn solution. Call LoadSolutionAsync first.");
}