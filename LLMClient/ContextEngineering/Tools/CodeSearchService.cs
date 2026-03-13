// File: LLMClient/ContextEngineering/Tools/CodeSearchService.cs

using System.Text.RegularExpressions;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools.Models;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;

namespace LLMClient.ContextEngineering.Tools;

internal sealed class CodeSearchService
{
    private readonly SolutionContext _context;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ILogger<CodeSearchService>? _logger;

    // 直接通过 SolutionContext 访问索引服务
    private SymbolIndexService IndexService => _context.Analyzer.IndexService;

    // 硬性上限，防止结果集过大
    private const int MaxTextSearchResults = 500;
    private const int MaxSemanticResults = 50;
    private const int MaxAttributeResults = 100;

    public CodeSearchService(
        SolutionContext context,
        IEmbeddingService? embeddingService = null,
        ILogger<CodeSearchService>? logger = null)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    // ── search_text ────────────────────────────────────────────────────────

    public TextSearchView SearchText(
        string pattern,
        string? scope = null,
        string? fileFilter = null,
        bool useRegex = false,
        int contextLines = 1)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new TextSearchView
            {
                Query = pattern ?? string.Empty,
                SearchMode = useRegex ? "Regex" : "Text",
                TotalMatches = 0,
                FilesSearched = 0,
                Truncated = false,
                Results = new List<TextSearchResult>()
            };
        }

        var info = _context.RequireSolutionInfoOrThrow();
        var solutionDir = _context.RequireSolutionDirOrThrow();

        // 确定搜索范围（所有或特定目录）
        var searchRoot = string.IsNullOrWhiteSpace(scope)
            ? solutionDir
            : _context.ResolveToAbsolute(scope);

        // 构建文件过滤器
        var extensions = ParseFileFilter(fileFilter);

        var results = new List<TextSearchResult>();
        var filesSearched = 0;

        // 遍历所有已索引文件
        foreach (var project in info.Projects)
        {
            foreach (var file in project.Files)
            {
                if (!file.FilePath.StartsWith(searchRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (extensions.Any() && !extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                    continue;

                filesSearched++;

                try
                {
                    var matches = SearchInFileInternal(
                        file.FilePath,
                        pattern,
                        useRegex,
                        contextLines);

                    results.AddRange(matches.Select(m => new TextSearchResult
                    {
                        FilePath = m.filePath,
                        RelativePath = _context.ToSolutionRelative(m.filePath),
                        LineNumber = m.lineNumber,
                        Column = m.column,
                        LineContent = m.lineContent,
                        ContextBefore = m.contextBefore,
                        ContextAfter = m.contextAfter
                    }));

                    if (results.Count >= MaxTextSearchResults)
                        break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Failed to search file {Path}: {Msg}", file.FilePath, ex.Message);
                }
            }

            if (results.Count >= MaxTextSearchResults)
                break;
        }

        return new TextSearchView
        {
            Query = pattern,
            SearchMode = useRegex ? "Regex" : "Text",
            TotalMatches = results.Count,
            FilesSearched = filesSearched,
            Truncated = results.Count >= MaxTextSearchResults,
            Results = results.Take(MaxTextSearchResults).ToList()
        };
    }

    // ── search_semantic ────────────────────────────────────────────────────

    public async Task<SemanticSearchView> SearchSemanticAsync(
        string query,
        int topK = 20,
        CancellationToken ct = default)
    {
        topK = Math.Clamp(topK, 1, MaxSemanticResults);

        // 优先使用 embedding 服务（RAG）
        if (_embeddingService != null)
        {
            try
            {
                var embResults = await _embeddingService.SearchByEmbeddingAsync(query, topK, ct);

                var results = embResults.Select(r =>
                {
                    var (symbolId, symbolName, summary) = TryResolveSymbol(r.filePath, r.startLine);

                    return new SemanticSearchResult
                    {
                        FilePath = r.filePath,
                        RelativePath = _context.ToSolutionRelative(r.filePath),
                        StartLine = r.startLine,
                        EndLine = r.endLine,
                        CodeSnippet = r.snippet,
                        SimilarityScore = Math.Round(r.score, 3),
                        SymbolId = symbolId,
                        Name = symbolName,
                        Summary = summary
                    };
                }).ToList();

                return new SemanticSearchView
                {
                    Query = query,
                    TotalResults = results.Count,
                    Results = results,
                    Source = "RAG"
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Embedding search failed, falling back to text search: {Msg}", ex.Message);
            }
        }

        // Fallback：降级为关键词文本搜索
        var textView = SearchText(query);
        var fallbackResults = textView.Results
            .Take(topK)
            .Select(r =>
            {
                var (symbolId, symbolName, summary) = TryResolveSymbol(r.FilePath, r.LineNumber);

                return new SemanticSearchResult
                {
                    FilePath = r.FilePath,
                    RelativePath = r.RelativePath,
                    StartLine = r.LineNumber,
                    EndLine = r.LineNumber,
                    CodeSnippet = r.LineContent,
                    SimilarityScore = 0,
                    SymbolId = symbolId,
                    Name = symbolName,
                    Summary = summary
                };
            })
            .ToList();

        return new SemanticSearchView
        {
            Query = query,
            TotalResults = fallbackResults.Count,
            Results = fallbackResults,
            Source = "Fallback"
        };
    }

    // ── find_similar_code ──────────────────────────────────────────────────

    public async Task<SemanticSearchView> FindSimilarCodeAsync(
        string codeSnippet,
        int topK = 10,
        CancellationToken ct = default)
    {
        // 复用 search_semantic，将代码片段作为查询
        return await SearchSemanticAsync(codeSnippet, topK, ct);
    }

    // ── find_by_attribute ──────────────────────────────────────────────────

    public AttributeSearchView FindByAttribute(string attributeName, string? scope = null)
    {
        _context.RequireSolutionInfoOrThrow();

        // 规范化特性名称（移除 "Attribute" 后缀）
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase)
            ? attributeName.Substring(0, attributeName.Length - 9)
            : attributeName;

        var results = new List<AttributeSearchResult>();

        // 直接遍历索引中的所有符号（无需嵌套循环遍历 SolutionInfo）
        var allSymbols = GetAllSymbolsFromIndex();

        foreach (var sym in allSymbols)
        {
            // scope 过滤：如果指定了 scope，检查符号是否在该范围内
            if (!string.IsNullOrWhiteSpace(scope))
            {
                var inScope = sym.FilesPath.Any(fp =>
                                  fp.Contains(scope, StringComparison.OrdinalIgnoreCase)) ||
                              sym.Signature.Contains(scope, StringComparison.OrdinalIgnoreCase);

                if (!inScope)
                    continue;
            }

            // 检查是否具有目标特性
            if (HasAttribute(sym.Attributes, normalizedName))
            {
                results.Add(MapToAttributeSearchResult(sym));

                if (results.Count >= MaxAttributeResults)
                    break;
            }
        }

        return new AttributeSearchView
        {
            AttributeName = attributeName,
            TotalCount = results.Count,
            Results = results
        };
    }

    // ── search_in_file ─────────────────────────────────────────────────────

    public TextSearchView SearchInFile(
        string filePath,
        string pattern,
        bool useRegex = false,
        int contextLines = 1)
    {
        var absPath = _context.ResolveToAbsolute(filePath);

        if (!File.Exists(absPath))
            throw new FileNotFoundException($"File not found: {filePath}");

        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new TextSearchView
            {
                Query = pattern ?? string.Empty,
                SearchMode = useRegex ? "Regex" : "Text",
                TotalMatches = 0,
                FilesSearched = 1,
                Truncated = false,
                Results = []
            };
        }

        var matches = SearchInFileInternal(absPath, pattern, useRegex, contextLines);

        return new TextSearchView
        {
            Query = pattern,
            SearchMode = useRegex ? "Regex" : "Text",
            TotalMatches = matches.Count,
            FilesSearched = 1,
            Truncated = false,
            Results = matches.Select(m => new TextSearchResult
            {
                FilePath = m.filePath,
                RelativePath = _context.ToSolutionRelative(m.filePath),
                LineNumber = m.lineNumber,
                Column = m.column,
                LineContent = m.lineContent,
                ContextBefore = m.contextBefore,
                ContextAfter = m.contextAfter
            }).ToList()
        };
    }

    // ── 内部辅助方法 ────────────────────────────────────────────────────────

    private List<(string filePath, int lineNumber, int column, string lineContent, string? contextBefore, string?
            contextAfter)>
        SearchInFileInternal(string filePath, string pattern, bool useRegex, int contextLines)
    {
        var results = new List<(string, int, int, string, string?, string?)>();

        // ✅ 防御性检查（虽然上层已检查，但这里再检查一次更安全）
        if (string.IsNullOrWhiteSpace(pattern))
            return results;

        var lines = File.ReadAllLines(filePath);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            bool isMatch;

            if (useRegex)
            {
                try
                {
                    var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                    isMatch = match.Success;

                    if (isMatch)
                    {
                        var column = match.Index + 1;
                        var contextBefore = ExtractContextBefore(lines, i, contextLines);
                        var contextAfter = ExtractContextAfter(lines, i, contextLines);
                        results.Add((filePath, i + 1, column, line.Trim(), contextBefore, contextAfter));
                    }
                }
                catch (ArgumentException)
                {
                    // 无效的正则表达式，跳过此行
                    continue;
                }
            }
            else
            {
                var index = line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                isMatch = index >= 0;

                if (isMatch)
                {
                    var column = index + 1;
                    var contextBefore = ExtractContextBefore(lines, i, contextLines);
                    var contextAfter = ExtractContextAfter(lines, i, contextLines);
                    results.Add((filePath, i + 1, column, line.Trim(), contextBefore, contextAfter));
                }
            }
        }

        return results;
    }

    private static string? ExtractContextBefore(string[] lines, int currentIndex, int contextLines)
    {
        if (contextLines <= 0 || currentIndex <= 0)
            return null;

        var startIndex = Math.Max(0, currentIndex - contextLines);
        var count = currentIndex - startIndex;
        return string.Join("\n", lines.Skip(startIndex).Take(count));
    }

    private static string? ExtractContextAfter(string[] lines, int currentIndex, int contextLines)
    {
        if (contextLines <= 0 || currentIndex >= lines.Length - 1)
            return null;

        var count = Math.Min(contextLines, lines.Length - currentIndex - 1);
        return string.Join("\n", lines.Skip(currentIndex + 1).Take(count));
    }


    private (string? symbolId, string? symbolName, string? summary) TryResolveSymbol(
        string filePath,
        int lineNumber)
    {
        var sym = IndexService.FindByLocation(filePath, lineNumber);

        return sym != null
            ? (sym.SymbolId, sym.Name, sym.Summary)
            : (null, null, null);
    }


    // ── 新增：从索引获取所有符号 ────────────────────────────────────────

    /// <summary>
    /// 从索引服务获取所有已索引的符号（内存中已加载，性能优于遍历 SolutionInfo）
    /// </summary>
    private IEnumerable<SymbolInfo> GetAllSymbolsFromIndex()
    {
        // 利用索引的 Search 方法获取所有符号
        // 传入空字符串查询会返回所有符号（ScoreSymbol 会返回 0，但我们不关心分数）
        // 更高效的方式是直接暴露 _keyIndex.Values，但当前 API 下可以这样做：

        // 方案1：使用 Search 返回所有（如果 query 为空会返回空集，需要特殊处理）
        // 方案2：直接通过反射或添加新 API 获取 _keyIndex.Values

        // 推荐：在 SymbolIndexService 中添加一个 GetAllSymbols() 方法
        return IndexService.GetAllSymbols();
    }

    private static bool IsInLocation(SymbolInfo symbol, string filePath, int lineNumber)
    {
        return symbol.Locations.Any(loc =>
            string.Equals(loc.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
            lineNumber >= loc.Location.Start.Line &&
            lineNumber <= loc.Location.End.Line);
    }

    private static bool HasAttribute(List<string> attributes, string targetName)
    {
        return attributes.Any(attr =>
            attr.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
            attr.Equals(targetName + "Attribute", StringComparison.OrdinalIgnoreCase));
    }

    private AttributeSearchResult MapToAttributeSearchResult(SymbolInfo symbol)
    {
        var firstLocation = symbol.Locations.FirstOrDefault();

        return new AttributeSearchResult
        {
            SymbolId = symbol.SymbolId,
            Name = symbol.Name,
            Kind = symbol.Kind,
            Signature = symbol.Signature,
            Accessibility = symbol.Accessibility,
            Attributes = symbol.Attributes.ToList(),
            Summary = symbol.Summary,
            Location = firstLocation != null
                ? new LocationView
                {
                    FilePath = firstLocation.FilePath,
                    StartLine = firstLocation.Location.Start.Line,
                    EndLine = firstLocation.Location.End.Line
                }
                : null
        };
    }

    private static List<string> ParseFileFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new List<string>();

        return filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext : "." + ext)
            .ToList();
    }
}