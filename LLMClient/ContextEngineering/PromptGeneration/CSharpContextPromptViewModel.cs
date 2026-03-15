using LLMClient.Abstraction;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.Project;

namespace LLMClient.ContextEngineering.PromptGeneration;

/// <summary>
/// C#项目代码上下文提示构建器
/// </summary>
public class CSharpContextPromptViewModel : ContextPromptViewModel<CSharpProjectViewModel>
{
    public FocusedContext? FocusedContext { get; set; }

    public List<RelevantSnippet>? RelevantSnippets { get; set; }

    public CSharpContextPromptViewModel(SolutionContext context, CSharpProjectViewModel projectViewModel,
        ITokensCounter tokensCounter)
        : base(projectViewModel, tokensCounter)
    {
        _context = context;
    }

    protected override async Task<string> BuildProjectContextAsync()
    {
        var markdownSummaryFormatter = new MarkdownSummaryFormatter(new FormatterOptions()
        {
            IncludeMembers = true,
            IncludePackages = true
        });

        var solutionInfo = _context.RequireSolutionInfoOrThrow();
        var projectContext = markdownSummaryFormatter.Format(solutionInfo);
        return await PromptTemplateRenderer.RenderHandlebarsAsync(
            ContextPromptTemplates.ProjectStructureTemplate,
            new Dictionary<string, object?>
            {
                ["projectStructure"] = projectContext,
                ["ProjectStructureGuideLines"] = ContextPromptTemplates.ProjectStructureGuideLines,
            });
    }


    protected override async Task<string> BuildFocusedContextAsync()
    {
        if (FocusedContext is null) return string.Empty;

        var variables = new Dictionary<string, object?>
        {
            ["filePath"] = FocusedContext.FilePath,
            //todo: add doc
        };

        return await PromptTemplateRenderer.RenderAsync(
            ContextPromptTemplates.FocusedContextTemplate,
            variables);
    }

    protected override async Task<string> BuildRelevantSnippetsAsync()
    {
        if (RelevantSnippets == null || RelevantSnippets.Count == 0) return string.Empty;

        var snippetStrings = new List<string>();
        foreach (var snippet in RelevantSnippets.Take(5)) // 限制数量
        {
            var variables = new Dictionary<string, object?>
            {
                ["sourcePath"] = snippet.SourcePath,
                ["relevanceScore"] = snippet.RelevanceScore.ToString("F2"),
                ["signature"] = snippet.Signature,
                ["summary"] = snippet.Summary ?? "",
                ["codeContent"] = snippet.CodeContent
            };

            var rendered = await PromptTemplateRenderer.RenderAsync(
                ContextPromptTemplates.CodeSnippetTemplate,
                variables);
            snippetStrings.Add(rendered);
        }

        var allSnippets = string.Join("\n", snippetStrings);

        var mainVariables = new Dictionary<string, object?>
        {
            ["searchQuery"] = RelevantSnippets.FirstOrDefault()?.Query ?? "user query",
            ["snippetsList"] = allSnippets
        };

        return await PromptTemplateRenderer.RenderAsync(
            ContextPromptTemplates.RelevantSnippetsTemplate,
            mainVariables);
    }

    private readonly SolutionContext _context;

    public override void Dispose()
    {
        base.Dispose();
    }
}