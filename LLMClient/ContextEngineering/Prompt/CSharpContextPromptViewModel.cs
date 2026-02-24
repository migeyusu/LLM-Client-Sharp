using LLMClient.Abstraction;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.Project;

namespace LLMClient.ContextEngineering.Prompt;

/// <summary>
/// C#项目代码上下文提示构建器
/// </summary>
public class CSharpContextPromptViewModel : ContextPromptViewModel<CSharpProjectViewModel>
{
    public FocusedContext? FocusedContext { get; set; }

    public List<RelevantSnippet>? RelevantSnippets { get; set; }

    public ProjectInfo? ProjectInfo { get; set; }

    public SolutionInfo? SolutionInfo { get; set; }

    public CSharpContextPromptViewModel(RoslynProjectAnalyzer analyzer, CSharpProjectViewModel projectViewModel,
        ITokensCounter tokensCounter)
        : base(projectViewModel, tokensCounter)
    {
        _analyzer = analyzer;
    }

    protected override async Task<string> BuildProjectContextAsync()
    {
        string projectContext;
        var markdownSummaryFormatter = new MarkdownSummaryFormatter(new FormatterOptions()
        {
            IncludeMembers = true,
            IncludePackages = true
        });
        if (ProjectViewModel.IsSolutionMode)
        {
            if (string.IsNullOrEmpty(ProjectViewModel.SolutionFilePath))
            {
                throw new NotSupportedException("Solution file path cannot be null or empty.");
            }

            var solutionInfo = await _analyzer.AnalyzeSolutionAsync(ProjectViewModel.SolutionFilePath);
            this.SolutionInfo = solutionInfo;
            this.ProjectInfo = null;
        }
        else
        {
            if (string.IsNullOrEmpty(ProjectViewModel.ProjectFilePath))
            {
                throw new NotSupportedException("Project file path cannot be null or empty.");
            }

            var projectInfo = await _analyzer.AnalyzeProjectAsync(ProjectViewModel.ProjectFilePath);
            this.ProjectInfo = projectInfo;
            this.SolutionInfo = null;
        }

        if (ProjectInfo != null)
        {
            projectContext = markdownSummaryFormatter.Format(ProjectInfo);
        }
        else if (SolutionInfo != null)
        {
            projectContext = markdownSummaryFormatter.Format(SolutionInfo);
        }
        else
        {
            return string.Empty;
        }

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

    private readonly RoslynProjectAnalyzer _analyzer;

    public override void Dispose()
    {
        _analyzer.Dispose();
        base.Dispose();
    }
}