// CodeReadingService.cs
using AutoMapper;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using SymbolInfo = LLMClient.ContextEngineering.Analysis.SymbolInfo;

namespace LLMClient.ContextEngineering.Tools;

internal sealed class CodeReadingService : ICodeReadingService
{
    private readonly SolutionContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<CodeReadingService>? _logger;

    /// <summary>粗略估算：4字符约1个token</summary>
    private const int CharsPerToken = 4;
    private const int DefaultMaxTokens = 8000;
    private const int MaxFileListCount = 500;

    public CodeReadingService(SolutionContext context, IMapper mapper, ILogger<CodeReadingService>? logger = null)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    // ── read_file ─────────────────────────────────────────────────────────

    public ReadFileResult ReadFile(
        string path,
        int? startLine = null,
        int? endLine = null,
        int? maxTokens = null)
    {
        var absPath = _context.ResolveToAbsolute(path);
        if (!File.Exists(absPath))
            throw new FileNotFoundException($"File not found: {path}");

        var lines = File.ReadAllLines(absPath);
        var totalLines = lines.Length;
        var tokenLimit = maxTokens ?? DefaultMaxTokens;

        // 转换到 0-based 索引，做边界保护
        var fromIdx = Math.Max(0, (startLine ?? 1) - 1);
        var toIdx   = Math.Min(totalLines - 1, (endLine ?? totalLines) - 1);

        if (fromIdx > toIdx)
            throw new ArgumentException(
                $"startLine ({startLine}) must be ≤ endLine ({endLine}) and within file range [1, {totalLines}].");

        var selectedLines = lines[fromIdx..(toIdx + 1)];
        var content = string.Join(Environment.NewLine, selectedLines);
        var tokenEstimate = content.Length / CharsPerToken;
        var truncated = false;

        if (tokenEstimate > tokenLimit)
        {
            // 按行边界截断，保持代码完整性
            var budget = tokenLimit * CharsPerToken;
            var sb = new System.Text.StringBuilder(capacity: budget);
            var includedLines = 0;

            foreach (var line in selectedLines)
            {
                var needed = line.Length + Environment.NewLine.Length;
                if (sb.Length + needed > budget) break;
                sb.AppendLine(line);
                includedLines++;
            }

            content = sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
            toIdx = fromIdx + includedLines - 1;
            tokenEstimate = content.Length / CharsPerToken;
            truncated = true;
        }

        return new ReadFileResult
        {
            FilePath = absPath,
            RelativePath = _context.ToSolutionRelative(absPath),
            TotalLines = totalLines,
            StartLine = fromIdx + 1,
            EndLine = toIdx + 1,
            Content = content,
            Truncated = truncated,
            TokenEstimate = tokenEstimate
        };
    }

    // ── read_symbol_body ──────────────────────────────────────────────────

    public async Task<SymbolBodyView> ReadSymbolBodyAsync(
        string symbolId,
        int contextLines = 0,
        CancellationToken ct = default)
    {
        var sym = _context.Analyzer.IndexService.GetByKey(symbolId)
                  ?? throw new ArgumentException(
                      $"Symbol '{symbolId}' not found. Use search_symbols to discover valid IDs.");

        var location = sym.Locations.FirstOrDefault()
                       ?? throw new InvalidOperationException(
                           $"Symbol '{symbolId}' has no source location.");

        var filePath = location.FilePath;
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Source file not found: {filePath}");

        // 优先用 Roslyn 获取语法节点的完整 span（含方法体）
        var (bodyStart, bodyEnd, source) = await TryGetFullBodySpanAsync(sym, filePath, ct);

        // 回退到索引中存储的行范围（仅签名起止行）
        if (bodyStart <= 0)
        {
            bodyStart = location.Location.Start.Line; // 已是 1-based
            bodyEnd   = location.Location.End.Line;
            source    = "Index";
        }

        var fileLines = await File.ReadAllLinesAsync(filePath, ct);
        var totalLines = fileLines.Length;

        contextLines = Math.Max(0, contextLines);
        var contentStart = Math.Max(1, bodyStart - contextLines);
        var contentEnd   = Math.Min(totalLines, bodyEnd + contextLines);

        var selectedLines = fileLines[(contentStart - 1)..contentEnd];
        var content = string.Join(Environment.NewLine, selectedLines);

        return new SymbolBodyView
        {
            SymbolId       = sym.SymbolId,
            SymbolName     = sym.Name,
            Signature      = sym.Signature,
            FilePath       = filePath,
            RelativePath   = _context.ToSolutionRelative(filePath),
            BodyStartLine  = bodyStart,
            BodyEndLine    = bodyEnd,
            ContentStartLine = contentStart,
            ContentEndLine   = contentEnd,
            Content        = content,
            TokenEstimate  = content.Length / CharsPerToken,
            Source         = source
        };
    }

    /// <summary>
    /// 通过 Roslyn 语法树精确定位声明节点（含方法体）的行范围。
    /// 基于索引中保存的 1-based 起始行定位文件内的语法节点。
    /// </summary>
    private async Task<(int start, int end, string source)> TryGetFullBodySpanAsync(
        SymbolInfo sym,
        string filePath,
        CancellationToken ct)
    {
        if (!_context.IsLoaded) return (0, 0, "Index");

        try
        {
            var solution = _context.RequireRoslynSolutionOrThrow();

            var docId = solution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            if (docId == null) return (0, 0, "Index");

            var document = solution.GetDocument(docId);
            if (document == null) return (0, 0, "Index");

            var root = await document.GetSyntaxRootAsync(ct);
            var text = await document.GetTextAsync(ct);
            if (root == null) return (0, 0, "Index");

            var firstLoc = sym.Locations.First();
            // 索引存储 1-based，转换为 0-based 获取 TextLine
            var zeroBasedLine = firstLoc.Location.Start.Line - 1;
            if (zeroBasedLine < 0 || zeroBasedLine >= text.Lines.Count)
                return (0, 0, "Index");

            var lineSpan = text.Lines[zeroBasedLine].Span;
            var node = root.FindNode(lineSpan, getInnermostNodeForTie: true);

            // 向上查找最近的成员或类型声明节点
            var declNode = node.AncestorsAndSelf().FirstOrDefault(static n =>
                n is MemberDeclarationSyntax or
                BaseTypeDeclarationSyntax or
                DelegateDeclarationSyntax);

            if (declNode == null) return (0, 0, "Index");

            var linePos = declNode.GetLocation().GetLineSpan();
            // 返回 1-based
            return (
                linePos.StartLinePosition.Line + 1,
                linePos.EndLinePosition.Line + 1,
                "Roslyn"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                "Failed to resolve body span via Roslyn for '{Id}': {Msg}", sym.SymbolId, ex.Message);
            return (0, 0, "Index");
        }
    }

    // ── get_file_outline ──────────────────────────────────────────────────

    public FileOutlineView GetFileOutline(string path)
    {
        var absPath = _context.ResolveToAbsolute(path);
        if (!File.Exists(absPath))
            throw new FileNotFoundException($"File not found: {path}");

        var info = _context.RequireSolutionInfoOrThrow();

        // 行数由文件系统提供，不走 Roslyn（避免重新解析）
        var totalLines = 0;
        try { totalLines = File.ReadAllLines(absPath).Length; }
        catch (Exception ex)
        {
            _logger?.LogWarning("Could not count lines for '{Path}': {Msg}", absPath, ex.Message);
        }

        // 从索引中提取归属于该文件的命名空间/类型/成员
        var nsViews = info.Projects
            .SelectMany(p => p.Namespaces)
            .Select(ns => new
            {
                Namespace = ns,
                Types = ns.Types
                    .Where(t => t.Locations.Any(l =>
                        string.Equals(l.FilePath, absPath, StringComparison.OrdinalIgnoreCase)))
                    .ToList()
            })
            .Where(x => x.Types.Count > 0)
            .Select(x => new NamespaceOutlineView
            {
                Name  = x.Namespace.Name,
                Types = x.Types
                    .Select(t =>
                    {
                        var typeLocs = t.Locations
                            .Where(l => string.Equals(l.FilePath, absPath, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        var typeStart = typeLocs.Count > 0
                            ? typeLocs.Min(l => l.Location.Start.Line) : 0;
                        var typeEnd = typeLocs.Count > 0
                            ? typeLocs.Max(l => l.Location.End.Line) : 0;

                        var mappedType = _mapper.Map<TypeOutlineView>(t);
                        var memberViews = t.Members
                            .Where(m => m.Locations.Any(l =>
                                string.Equals(l.FilePath, absPath, StringComparison.OrdinalIgnoreCase)))
                            .Select(m =>
                            {
                                var memberLoc = m.Locations
                                    .Where(l => string.Equals(l.FilePath, absPath, StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(l => l.Location.Start.Line)
                                    .FirstOrDefault();
                                var mappedMember = _mapper.Map<MemberOutlineView>(m);
                                return new MemberOutlineView
                                {
                                    SymbolId = mappedMember.SymbolId,
                                    Name = mappedMember.Name,
                                    Kind = mappedMember.Kind,
                                    Signature = mappedMember.Signature,
                                    Accessibility = mappedMember.Accessibility,
                                    Summary = mappedMember.Summary,
                                    StartLine = memberLoc?.Location.Start.Line ?? 0
                                };
                            })
                            .OrderBy(m => m.StartLine)
                            .ToList();

                        return new TypeOutlineView
                        {
                            SymbolId = mappedType.SymbolId,
                            Name = mappedType.Name,
                            Kind = mappedType.Kind,
                            Signature = mappedType.Signature,
                            Accessibility = mappedType.Accessibility,
                            Summary = mappedType.Summary,
                            StartLine = typeStart,
                            EndLine = typeEnd,
                            Members = memberViews
                        };
                    })
                    .OrderBy(t => t.StartLine)
                    .ToList()
            })
            .ToList();

        return new FileOutlineView
        {
            FilePath     = absPath,
            RelativePath = _context.ToSolutionRelative(absPath),
            TotalLines   = totalLines,
            Namespaces   = nsViews
        };
    }

    // ── list_files ────────────────────────────────────────────────────────

    public FileListResult ListFiles(
        string path = ".",
        string? filter = null,
        bool recursive = true,
        int maxCount = 300)
    {
        var info = _context.RequireSolutionInfoOrThrow();
        var absRoot = _context.ResolveToAbsolute(path);

        maxCount = Math.Clamp(maxCount, 1, MaxFileListCount);

        var query = info.Projects
            .SelectMany(p => p.Files)
            .Where(f => f.FilePath.StartsWith(absRoot, StringComparison.OrdinalIgnoreCase));

        // 非递归：仅当前目录下直接文件（不含子目录）
        if (!recursive)
        {
            query = query.Where(f =>
                string.Equals(
                    Path.GetDirectoryName(f.FilePath),
                    absRoot,
                    StringComparison.OrdinalIgnoreCase));
        }

        // 过滤：支持逗号分隔的扩展名（".cs"）或文件名子串（"Service"）
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var patterns = filter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            query = query.Where(f => patterns.Any(pat =>
                Path.GetFileName(f.FilePath).Contains(pat, StringComparison.OrdinalIgnoreCase) ||
                f.Extension.Equals(pat.TrimStart('*'), StringComparison.OrdinalIgnoreCase)));
        }

        var all = query.OrderBy(f => f.FilePath).ToList();
        var truncated = all.Count > maxCount;

        var files = all
            .Take(maxCount)
            .Select(f =>
            {
                var view = _mapper.Map<FileMetadataView>(f);
                view.RelativePath = _context.ToSolutionRelative(f.FilePath);
                return view;
            })
            .ToList();

        return new FileListResult
        {
            RootPath   = _context.ToSolutionRelative(absRoot),
            TotalCount = all.Count,
            Truncated  = truncated,
            Files      = files
        };
    }
    
}