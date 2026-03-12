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
                        SymbolName = symbolName,
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
                    SymbolName = symbolName,
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
        var info = _context.RequireSolutionInfoOrThrow();

        // 规范化特性名称（移除 "Attribute" 后缀）
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase)
            ? attributeName.Substring(0, attributeName.Length - 9)
            : attributeName;

        var results = new List<AttributeSearchResult>();

        // 遍历所有符号，查找标注了该特性的
        foreach (var project in info.Projects)
        {
            // 可选：限制搜索范围到特定项目
            if (!string.IsNullOrWhiteSpace(scope) &&
                !project.Name.Equals(scope, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var ns in project.Namespaces)
            {
                // 搜索类型级别的特性
                foreach (var type in ns.Types)
                {
                    if (HasAttribute(type.Attributes, normalizedName))
                    {
                        results.Add(MapToAttributeSearchResult(type));
                    }

                    // 搜索成员级别的特性
                    foreach (var member in type.Members)
                    {
                        if (HasAttribute(member.Attributes, normalizedName))
                        {
                            results.Add(MapToAttributeSearchResult(member));
                        }

                        if (results.Count >= MaxAttributeResults)
                            goto done;
                    }
                }
            }
        }

        done:
        return new AttributeSearchView
        {
            AttributeName = attributeName,
            TotalCount = results.Count,
            Results = results.Take(MaxAttributeResults).ToList()
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
        var lines = File.ReadAllLines(filePath);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var matchIndex = -1;

            if (useRegex)
            {
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    matchIndex = match.Index;
            }
            else
            {
                matchIndex = line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            }

            if (matchIndex < 0)
                continue;

            var column = matchIndex + 1;

            var contextBefore = contextLines > 0 && i > 0
                ? string.Join("\n", lines.Skip(Math.Max(0, i - contextLines)).Take(contextLines))
                : null;

            var contextAfter = contextLines > 0 && i < lines.Length - 1
                ? string.Join("\n", lines.Skip(i + 1).Take(Math.Min(contextLines, lines.Length - i - 1)))
                : null;

            results.Add((filePath, i + 1, column, line.Trim(), contextBefore, contextAfter));
        }

        return results;
    }

    private (string? symbolId, string? symbolName, string? summary) TryResolveSymbol(string filePath, int lineNumber)
    {
        var info = _context.RequireSolutionInfoOrThrow();

        // 尝试从索引中找到该位置对应的符号
        foreach (var project in info.Projects)
        {
            foreach (var ns in project.Namespaces)
            {
                foreach (var type in ns.Types)
                {
                    if (IsInLocation(type, filePath, lineNumber))
                        return (type.SymbolId, type.Name, type.Summary);

                    foreach (var member in type.Members)
                    {
                        if (IsInLocation(member, filePath, lineNumber))
                            return (member.SymbolId, member.Name, member.Summary);
                    }
                }
            }
        }

        return (null, null, null);
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