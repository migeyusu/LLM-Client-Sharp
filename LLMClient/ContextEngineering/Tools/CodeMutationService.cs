using System.Text;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.Tools.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.Extensions.Logging;

namespace LLMClient.ContextEngineering.Tools;

/// <summary>
/// Roslyn-based code mutation service. All operations are syntax-aware and write back to disk
/// via <see cref="RoslynProjectAnalyzer.ApplySolutionChanges"/>.
/// Symbol IDs consumed by this service follow the same convention as SymbolSemanticService:
/// <see cref="SymbolInfo.SymbolId"/> = UniqueId (Documentation Comment ID) or Signature fallback.
/// 
/// Error handling follows the same pattern as CodeSearchService and SymbolSemanticService:
/// all failure paths throw exceptions; FunctionCallEngine captures and formats them.
/// 
/// Before any change is written, a diff preview is built and passed to
/// <see cref="RequestDiffApprovalCallback"/> for user confirmation.
/// </summary>
public sealed class CodeMutationService
{
    private readonly SolutionContext _context;
    private readonly ILogger<CodeMutationService>? _logger;

    /// <summary>
    /// Optional callback invoked with a diff preview of every affected file.
    /// Return true to proceed and write changes; false to abort with UnauthorizedAccessException.
    /// </summary>
    public Func<List<CodeMutationFilePreview>, Task<bool>>? RequestDiffApprovalCallback { get; set; }

    /// <summary>
    /// Fallback callback used when diff preview cannot be built (e.g. old solution is unavailable).
    /// </summary>
    public Func<string, Task<bool>>? RequestPermissionCallback { get; set; }

    public CodeMutationService(SolutionContext context, ILogger<CodeMutationService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    // ── rename_symbol ─────────────────────────────────────────────────────

    public async Task<MutationResult> RenameSymbolAsync(
        string symbolId,
        string newName,
        CancellationToken ct = default)
    {
        var sym = ResolveSymbolOrThrow(symbolId);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct)
                        ?? throw new InvalidOperationException(
                            $"Could not resolve Roslyn symbol for '{symbolId}'.");

#pragma warning disable CS0618 // Renamer.RenameSymbolAsync overload with OptionSet is obsolete
        var newSolution = await Renamer.RenameSymbolAsync(solution, roslynSym, newName, null, ct);
#pragma warning restore CS0618

        await ApplyChangesOrThrowAsync(newSolution, "rename",
            [_context.ToSolutionRelative(sym.FilesPath.First())], ct);
        await ReanalyzeAsync(ct);

        return MutationResult.Ok([_context.ToSolutionRelative(sym.FilesPath.First())]);
    }

    // ── add_using ─────────────────────────────────────────────────────────

    public async Task<MutationResult> AddUsingAsync(
        string filePath,
        string namespaceName,
        CancellationToken ct = default)
    {
        var absPath = _context.ResolveToAbsolute(filePath);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var docId = solution.GetDocumentIdsWithFilePath(absPath).FirstOrDefault()
                    ?? throw new FileNotFoundException(
                        $"File '{filePath}' not found in the loaded solution.", absPath);

        var document = solution.GetDocument(docId)!;
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is not CompilationUnitSyntax compilationUnit)
            throw new InvalidOperationException(
                "File is not a compilation unit (e.g. top-level statements without a compilation root).");

        var normalizedNs = namespaceName.Trim();
        var existing = compilationUnit.Usings.Any(u => u.Name?.ToString() == normalizedNs);
        if (existing)
            return MutationResult.Ok([_context.ToSolutionRelative(absPath)], "Using directive already exists.");

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(normalizedNs));
        var newRoot = compilationUnit.AddUsings(newUsing);
        var newDoc = document.WithSyntaxRoot(newRoot);
        var newSolution = newDoc.Project.Solution;

        await ApplyChangesOrThrowAsync(newSolution, "add using",
            [_context.ToSolutionRelative(absPath)], ct);
        await ReanalyzeAsync(ct);

        return MutationResult.Ok([_context.ToSolutionRelative(absPath)]);
    }

    // ── delete_symbol ─────────────────────────────────────────────────────

    public async Task<MutationResult> DeleteSymbolAsync(
        string symbolId,
        CancellationToken ct = default)
    {
        var sym = ResolveSymbolOrThrow(symbolId);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct)
                        ?? throw new InvalidOperationException(
                            $"Could not resolve Roslyn symbol for '{symbolId}'.");

        var sourceLoc = roslynSym.Locations.FirstOrDefault(l => l.IsInSource)
                        ?? throw new InvalidOperationException(
                            "Symbol has no source location.");

        var doc = solution.GetDocument(sourceLoc.SourceTree)
                  ?? throw new InvalidOperationException(
                      "Could not locate document for the symbol.");

        var root = await doc.GetSyntaxRootAsync(ct);
        var node = root?.FindNode(sourceLoc.SourceSpan)
                   ?? throw new InvalidOperationException(
                       "Could not locate syntax node for the symbol.");

        var declNode = node.AncestorsAndSelf().FirstOrDefault(static n =>
            n is MemberDeclarationSyntax or
            BaseTypeDeclarationSyntax or
            DelegateDeclarationSyntax or
            EnumDeclarationSyntax)
            ?? throw new InvalidOperationException(
                "Could not locate a deletable declaration node.");

        var newRoot = root!.RemoveNode(declNode, SyntaxRemoveOptions.KeepNoTrivia)
                      ?? throw new InvalidOperationException(
                          "Removing the node would produce an invalid syntax tree.");

        var newDoc = doc.WithSyntaxRoot(newRoot);
        var newSolution = newDoc.Project.Solution;

        await ApplyChangesOrThrowAsync(newSolution, "delete",
            [_context.ToSolutionRelative(doc.FilePath!)], ct);
        await ReanalyzeAsync(ct);

        return MutationResult.Ok([_context.ToSolutionRelative(doc.FilePath!)]);
    }

    // ── replace_symbol_body ───────────────────────────────────────────────

    public async Task<MutationResult> ReplaceSymbolBodyAsync(
        string symbolId,
        string newBody,
        CancellationToken ct = default)
    {
        var sym = ResolveSymbolOrThrow(symbolId);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct)
                        ?? throw new InvalidOperationException(
                            $"Could not resolve Roslyn symbol for '{symbolId}'.");

        var sourceLoc = roslynSym.Locations.FirstOrDefault(l => l.IsInSource)
                        ?? throw new InvalidOperationException(
                            "Symbol has no source location.");

        var doc = solution.GetDocument(sourceLoc.SourceTree)
                  ?? throw new InvalidOperationException(
                      "Could not locate document for the symbol.");

        var root = await doc.GetSyntaxRootAsync(ct);
        var node = root?.FindNode(sourceLoc.SourceSpan)
                   ?? throw new InvalidOperationException(
                       "Could not locate syntax node for the symbol.");

        var declNode = node.AncestorsAndSelf().FirstOrDefault(static n =>
            n is MemberDeclarationSyntax or
            BaseTypeDeclarationSyntax or
            DelegateDeclarationSyntax or
            EnumDeclarationSyntax)
            ?? throw new InvalidOperationException(
                "Could not locate a replaceable declaration node.");

        // Parse the new body into a standalone compilation unit so we can extract the declaration
        var newTree = CSharpSyntaxTree.ParseText(newBody, cancellationToken: ct);
        var newParsed = await newTree.GetRootAsync(ct);
        var newDecl = newParsed.DescendantNodesAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault();

        if (newDecl == null)
        {
            // Also accept type declarations
            newDecl = newParsed.DescendantNodesAndSelf()
                .OfType<BaseTypeDeclarationSyntax>()
                .FirstOrDefault();
        }

        if (newDecl == null)
            throw new ArgumentException(
                "Provided newBody does not contain a valid member or type declaration. " +
                "Ensure the text includes the full declaration including modifiers and signature.");

        var newDocRoot = root!.ReplaceNode(declNode, newDecl);
        var newDoc = doc.WithSyntaxRoot(newDocRoot);
        var newSolution = newDoc.Project.Solution;

        await ApplyChangesOrThrowAsync(newSolution, "replace",
            [_context.ToSolutionRelative(doc.FilePath!)], ct);
        await ReanalyzeAsync(ct);

        return MutationResult.Ok([_context.ToSolutionRelative(doc.FilePath!)]);
    }

    // ── apply_semantic_edit ───────────────────────────────────────────────

    public async Task<MutationResult> ApplySemanticEditAsync(
        string filePath,
        List<SemanticEditOperation> edits,
        CancellationToken ct = default)
    {
        if (edits == null || edits.Count == 0)
            return MutationResult.Ok([], "No edits provided.");

        var absPath = _context.ResolveToAbsolute(filePath);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var docId = solution.GetDocumentIdsWithFilePath(absPath).FirstOrDefault()
                    ?? throw new FileNotFoundException(
                        $"File '{filePath}' not found in the loaded solution.", absPath);

        var document = solution.GetDocument(docId)!;
        var root = await document.GetSyntaxRootAsync(ct)
                   ?? throw new InvalidOperationException(
                       "Could not get syntax root for the file.");

        var currentRoot = root;

        foreach (var edit in edits)
        {
            var kind = edit.Kind.Trim();
            switch (kind)
            {
                case "Replace":
                {
                    if (string.IsNullOrEmpty(edit.OldText) || edit.NewText == null)
                        throw new ArgumentException(
                            "Replace edit requires both oldText and newText.");

                    var matchNode = FindNodeByText(currentRoot, edit.OldText)
                                    ?? throw new InvalidOperationException(
                                        $"Could not find node matching oldText: {Summarize(edit.OldText)}");

                    var newTree = CSharpSyntaxTree.ParseText(edit.NewText, cancellationToken: ct);
                    var newParsed = await newTree.GetRootAsync(ct);
                    var replacement = newParsed.DescendantNodesAndSelf().FirstOrDefault() ?? newParsed;
                    currentRoot = currentRoot.ReplaceNode(matchNode, replacement);
                    break;
                }

                case "Delete":
                {
                    if (string.IsNullOrEmpty(edit.OldText))
                        throw new ArgumentException(
                            "Delete edit requires oldText.");

                    var matchNode = FindNodeByText(currentRoot, edit.OldText)
                                    ?? throw new InvalidOperationException(
                                        $"Could not find node matching oldText: {Summarize(edit.OldText)}");

                    var removed = currentRoot.RemoveNode(matchNode, SyntaxRemoveOptions.KeepNoTrivia)
                                  ?? throw new InvalidOperationException(
                                      "Deleting the node would produce an invalid syntax tree.");
                    currentRoot = removed;
                    break;
                }

                case "InsertBefore":
                case "InsertAfter":
                {
                    if (string.IsNullOrEmpty(edit.OldText) || edit.NewText == null)
                        throw new ArgumentException(
                            "Insert edit requires both oldText (anchor) and newText.");

                    var anchorNode = FindNodeByText(currentRoot, edit.OldText)
                                     ?? throw new InvalidOperationException(
                                         $"Could not find anchor node: {Summarize(edit.OldText)}");

                    var parent = anchorNode.Parent
                                 ?? throw new InvalidOperationException(
                                     "Anchor node has no parent.");

                    var newTree = CSharpSyntaxTree.ParseText(edit.NewText, cancellationToken: ct);
                    var newParsed = await newTree.GetRootAsync(ct);
                    var newNode = newParsed.DescendantNodesAndSelf().FirstOrDefault() ?? newParsed;

                    var newParent = kind == "InsertBefore"
                        ? parent.InsertNodesBefore(anchorNode, [newNode])
                        : parent.InsertNodesAfter(anchorNode, [newNode]);

                    currentRoot = currentRoot.ReplaceNode(parent, newParent);
                    break;
                }

                default:
                    throw new ArgumentException(
                        $"Unknown edit kind: {kind}. Accepted: Replace, Delete, InsertBefore, InsertAfter.");
            }
        }

        var newDoc = document.WithSyntaxRoot(currentRoot);
        var newSolution = newDoc.Project.Solution;

        await ApplyChangesOrThrowAsync(newSolution, "apply semantic edits",
            [_context.ToSolutionRelative(absPath)], ct);
        await ReanalyzeAsync(ct);

        return MutationResult.Ok([_context.ToSolutionRelative(absPath)]);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private LLMClient.ContextEngineering.Analysis.SymbolInfo ResolveSymbolOrThrow(string symbolId)
    {
        return _context.SymbolIndex.GetByKey(symbolId)
               ?? throw new ArgumentException(
                   $"Symbol '{symbolId}' not found. Use search_symbols to discover valid IDs.");
    }

    private static async Task<ISymbol?> ResolveRoslynSymbolAsync(
        LLMClient.ContextEngineering.Analysis.SymbolInfo sym,
        Solution solution,
        CancellationToken ct)
    {
        if (sym.UniqueId == null) return null;

        foreach (var project in solution.Projects)
        {
            var comp = await project.GetCompilationAsync(ct);
            if (comp == null) continue;

            var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(sym.UniqueId, comp);
            var found = symbols.FirstOrDefault();
            if (found != null) return found;
        }

        return null;
    }

    private async Task ApplyChangesOrThrowAsync(
        Solution newSolution,
        string operationName,
        List<string> affectedFiles,
        CancellationToken ct)
    {
        var oldSolution = _context.Analyzer.CurrentRawSolution;

        // 1) Try diff-aware confirmation first
        if (RequestDiffApprovalCallback != null && oldSolution != null)
        {
            var previews = await BuildFilePreviewsAsync(oldSolution, newSolution, ct);
            if (previews.Count > 0)
            {
                var approved = await RequestDiffApprovalCallback(previews);
                if (!approved)
                    throw new UnauthorizedAccessException(
                        $"Permission denied for {operationName}. User rejected the mutation.");
            }
        }
        // 2) Fallback to simple permission message
        else if (RequestPermissionCallback != null)
        {
            var permissionMsg = $"CodeMutation ({operationName}) on: {string.Join(", ", affectedFiles)}";
            var approved = await RequestPermissionCallback(permissionMsg);
            if (!approved)
                throw new UnauthorizedAccessException(
                    $"Permission denied for {operationName}. User rejected the mutation.");
        }

        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (!success)
            throw new InvalidOperationException(
                $"Workspace refused to apply {operationName} changes. " +
                "The target may be referenced in metadata, locked by another process, or the syntax tree became invalid.");
    }

    private async Task<List<CodeMutationFilePreview>> BuildFilePreviewsAsync(
        Solution oldSolution,
        Solution newSolution,
        CancellationToken ct)
    {
        var previews = new List<CodeMutationFilePreview>();

        try
        {
            var changes = newSolution.GetChanges(oldSolution);

            foreach (var projectChange in changes.GetProjectChanges())
            {
                foreach (var docId in projectChange.GetChangedDocuments())
                {
                    var oldDoc = oldSolution.GetDocument(docId);
                    var newDoc = newSolution.GetDocument(docId);
                    if (oldDoc == null || newDoc == null)
                        continue;

                    var oldText = await oldDoc.GetTextAsync(ct);
                    var newText = await newDoc.GetTextAsync(ct);
                    if (oldText.ContentEquals(newText))
                        continue;

                    var absPath = oldDoc.FilePath ?? newDoc.FilePath ?? string.Empty;
                    previews.Add(new CodeMutationFilePreview
                    {
                        RelativePath = _context.ToSolutionRelative(absPath),
                        AbsolutePath = absPath,
                        OriginalContent = oldText.ToString(),
                        UpdatedContent = newText.ToString()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                "Failed to build diff preview, falling back to simple permission: {Msg}", ex.Message);
        }

        return previews;
    }

    private static SyntaxNode? FindNodeByText(SyntaxNode root, string text)
    {
        var normalized = text.Trim();
        return root.DescendantNodesAndSelf().FirstOrDefault(n =>
            n.ToFullString().Trim() == normalized);
    }

    private static string Summarize(string text)
    {
        if (text.Length <= 80) return text;
        return text[..77] + "...";
    }

    private async Task ReanalyzeAsync(CancellationToken ct)
    {
        try
        {
            // Full re-analysis ensures SymbolIndex stays consistent.
            // Incremental refresh can be introduced later for performance.
            await _context.Analyzer.AnalysisCurrentSolutionAsync(ct);
            _logger?.LogInformation("Symbol index refreshed after mutation.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                "Symbol index refresh failed after mutation. Subsequent tool calls may use stale data: {Msg}",
                ex.Message);
        }
    }
}
