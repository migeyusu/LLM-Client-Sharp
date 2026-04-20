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
/// </summary>
public sealed class CodeMutationService
{
    private readonly SolutionContext _context;
    private readonly ILogger<CodeMutationService>? _logger;

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
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);
        if (roslynSym == null)
            return MutationResult.Fail($"Could not resolve Roslyn symbol for '{symbolId}'.");

#pragma warning disable CS0618 // Renamer.RenameSymbolAsync overload with OptionSet is obsolete
        var newSolution = await Renamer.RenameSymbolAsync(solution, roslynSym, newName, null, ct);
#pragma warning restore CS0618
        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (success)
        {
            await ReanalyzeAsync(ct);
        }

        return success
            ? MutationResult.Ok([_context.ToSolutionRelative(sym.FilesPath.First())])
            : MutationResult.Fail("Workspace refused to apply rename changes. The symbol may be referenced in metadata or locked files.");
    }

    // ── add_using ─────────────────────────────────────────────────────────

    public async Task<MutationResult> AddUsingAsync(
        string filePath,
        string namespaceName,
        CancellationToken ct = default)
    {
        var absPath = _context.ResolveToAbsolute(filePath);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var docId = solution.GetDocumentIdsWithFilePath(absPath).FirstOrDefault();
        if (docId == null)
            return MutationResult.Fail($"File '{filePath}' not found in the loaded solution.");

        var document = solution.GetDocument(docId)!;
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is not CompilationUnitSyntax compilationUnit)
            return MutationResult.Fail("File is not a compilation unit (e.g. top-level statements without a compilation root).");

        var normalizedNs = namespaceName.Trim();
        var existing = compilationUnit.Usings.Any(u => u.Name?.ToString() == normalizedNs);
        if (existing)
            return MutationResult.Ok([_context.ToSolutionRelative(absPath)], "Using directive already exists.");

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(normalizedNs));
        var newRoot = compilationUnit.AddUsings(newUsing);
        var newDoc = document.WithSyntaxRoot(newRoot);
        var newSolution = newDoc.Project.Solution;

        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (success) await ReanalyzeAsync(ct);

        return success
            ? MutationResult.Ok([_context.ToSolutionRelative(absPath)])
            : MutationResult.Fail("Workspace refused to apply using changes.");
    }

    // ── delete_symbol ─────────────────────────────────────────────────────

    public async Task<MutationResult> DeleteSymbolAsync(
        string symbolId,
        CancellationToken ct = default)
    {
        var sym = ResolveSymbolOrThrow(symbolId);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);
        if (roslynSym == null)
            return MutationResult.Fail($"Could not resolve Roslyn symbol for '{symbolId}'.");

        var sourceLoc = roslynSym.Locations.FirstOrDefault(l => l.IsInSource);
        if (sourceLoc == null)
            return MutationResult.Fail("Symbol has no source location.");

        var doc = solution.GetDocument(sourceLoc.SourceTree);
        if (doc == null)
            return MutationResult.Fail("Could not locate document for the symbol.");

        var root = await doc.GetSyntaxRootAsync(ct);
        var node = root?.FindNode(sourceLoc.SourceSpan);
        if (node == null)
            return MutationResult.Fail("Could not locate syntax node for the symbol.");

        var declNode = node.AncestorsAndSelf().FirstOrDefault(static n =>
            n is MemberDeclarationSyntax or
            BaseTypeDeclarationSyntax or
            DelegateDeclarationSyntax or
            EnumDeclarationSyntax);

        if (declNode == null)
            return MutationResult.Fail("Could not locate a deletable declaration node.");

        var newRoot = root!.RemoveNode(declNode, SyntaxRemoveOptions.KeepNoTrivia);
        if (newRoot == null)
            return MutationResult.Fail("Removing the node would produce an invalid syntax tree.");

        var newDoc = doc.WithSyntaxRoot(newRoot);
        var newSolution = newDoc.Project.Solution;

        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (success) await ReanalyzeAsync(ct);

        return success
            ? MutationResult.Ok([_context.ToSolutionRelative(doc.FilePath!)])
            : MutationResult.Fail("Workspace refused to apply deletion.");
    }

    // ── replace_symbol_body ───────────────────────────────────────────────

    public async Task<MutationResult> ReplaceSymbolBodyAsync(
        string symbolId,
        string newBody,
        CancellationToken ct = default)
    {
        var sym = ResolveSymbolOrThrow(symbolId);
        var solution = _context.RequireRoslynSolutionOrThrow();
        var roslynSym = await ResolveRoslynSymbolAsync(sym, solution, ct);
        if (roslynSym == null)
            return MutationResult.Fail($"Could not resolve Roslyn symbol for '{symbolId}'.");

        var sourceLoc = roslynSym.Locations.FirstOrDefault(l => l.IsInSource);
        if (sourceLoc == null)
            return MutationResult.Fail("Symbol has no source location.");

        var doc = solution.GetDocument(sourceLoc.SourceTree);
        if (doc == null)
            return MutationResult.Fail("Could not locate document for the symbol.");

        var root = await doc.GetSyntaxRootAsync(ct);
        var node = root?.FindNode(sourceLoc.SourceSpan);
        if (node == null)
            return MutationResult.Fail("Could not locate syntax node for the symbol.");

        var declNode = node.AncestorsAndSelf().FirstOrDefault(static n =>
            n is MemberDeclarationSyntax or
            BaseTypeDeclarationSyntax or
            DelegateDeclarationSyntax or
            EnumDeclarationSyntax);

        if (declNode == null)
            return MutationResult.Fail("Could not locate a replaceable declaration node.");

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
            return MutationResult.Fail(
                "Provided newBody does not contain a valid member or type declaration. " +
                "Ensure the text includes the full declaration including modifiers and signature.");

        var newDocRoot = root!.ReplaceNode(declNode, newDecl);
        var newDoc = doc.WithSyntaxRoot(newDocRoot);
        var newSolution = newDoc.Project.Solution;

        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (success) await ReanalyzeAsync(ct);

        return success
            ? MutationResult.Ok([_context.ToSolutionRelative(doc.FilePath!)])
            : MutationResult.Fail("Workspace refused to apply replacement.");
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
        var docId = solution.GetDocumentIdsWithFilePath(absPath).FirstOrDefault();
        if (docId == null)
            return MutationResult.Fail($"File '{filePath}' not found in the loaded solution.");

        var document = solution.GetDocument(docId)!;
        var root = await document.GetSyntaxRootAsync(ct);
        if (root == null)
            return MutationResult.Fail("Could not get syntax root for the file.");

        var currentRoot = root;

        foreach (var edit in edits)
        {
            var kind = edit.Kind.Trim();
            switch (kind)
            {
                case "Replace":
                {
                    if (string.IsNullOrEmpty(edit.OldText) || edit.NewText == null)
                        return MutationResult.Fail("Replace edit requires both oldText and newText.");

                    var matchNode = FindNodeByText(currentRoot, edit.OldText);
                    if (matchNode == null)
                        return MutationResult.Fail(
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
                        return MutationResult.Fail("Delete edit requires oldText.");

                    var matchNode = FindNodeByText(currentRoot, edit.OldText);
                    if (matchNode == null)
                        return MutationResult.Fail(
                            $"Could not find node matching oldText: {Summarize(edit.OldText)}");

                    var removed = currentRoot.RemoveNode(matchNode, SyntaxRemoveOptions.KeepNoTrivia);
                    if (removed == null)
                        return MutationResult.Fail("Deleting the node would produce an invalid syntax tree.");
                    currentRoot = removed;
                    break;
                }

                case "InsertBefore":
                case "InsertAfter":
                {
                    if (string.IsNullOrEmpty(edit.OldText) || edit.NewText == null)
                        return MutationResult.Fail(
                            "Insert edit requires both oldText (anchor) and newText.");

                    var anchorNode = FindNodeByText(currentRoot, edit.OldText);
                    if (anchorNode == null)
                        return MutationResult.Fail(
                            $"Could not find anchor node: {Summarize(edit.OldText)}");

                    var parent = anchorNode.Parent;
                    if (parent == null)
                        return MutationResult.Fail("Anchor node has no parent.");

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
                    return MutationResult.Fail($"Unknown edit kind: {kind}. Accepted: Replace, Delete, InsertBefore, InsertAfter.");
            }
        }

        var newDoc = document.WithSyntaxRoot(currentRoot);
        var newSolution = newDoc.Project.Solution;

        var success = _context.Analyzer.ApplySolutionChanges(newSolution);
        if (success) await ReanalyzeAsync(ct);

        return success
            ? MutationResult.Ok([_context.ToSolutionRelative(absPath)])
            : MutationResult.Fail("Workspace refused to apply semantic edits.");
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
