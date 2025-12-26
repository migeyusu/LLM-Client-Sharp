// ContextPromptBuilder.cs

using System.Text;

namespace LLMClient.ContextEngineering;

/// <summary>
/// 构建融合了代码上下文的 System Prompt
/// </summary>
public class ContextPromptBuilder
{
    private string _originalSystemPrompt = string.Empty;
    private FocusedContext? _focusedContext;
    private List<RelevantSnippet> _relevantSnippets = new();

    public ContextPromptBuilder()
    {
    }

    public ContextPromptBuilder WithOriginalSystemPrompt(string prompt)
    {
        _originalSystemPrompt = prompt;
        return this;
    }

    private ProjectInfo? _projectInfo;

    public ContextPromptBuilder WithProjectInfo(ProjectInfo projectInfo)
    {
        _projectInfo = projectInfo;
        return this;
    }

    private SolutionInfo? _solutionInfo;

    public ContextPromptBuilder WithSolutionInfo(SolutionInfo solutionInfo)
    {
        _solutionInfo = solutionInfo;
        return this;
    }

    public ContextPromptBuilder WithFocusedContext(FocusedContext context)
    {
        _focusedContext = context;
        return this;
    }

    public ContextPromptBuilder WithRelevantSnippets(IEnumerable<RelevantSnippet> snippets)
    {
        _relevantSnippets = snippets.ToList();
        return this;
    }

    public async Task<string> BuildAsync()
    {
        // 如果没有任何上下文，返回空
        if ((_solutionInfo is null && _projectInfo is null) && _focusedContext is null && _relevantSnippets.Count == 0)
        {
            return string.Empty;
        }

        var variables = new Dictionary<string, object?>
        {
            ["projectContext"] = BuildProjectSummaryAsync(),
            ["focusedContext"] = await BuildFocusedContextAsync(),
            ["relevantSnippets"] = await BuildRelevantSnippetsAsync()
        };

        return await PromptTemplateRenderer.RenderAsync(
            ContextPromptTemplates.CodeContextSectionTemplate,
            variables);
    }

    private string BuildProjectSummaryAsync()
    {
        var markdownSummaryFormatter = new MarkdownSummaryFormatter(new FormatterOptions()
        {
            IncludeMembers = true,
            IncludePackages = true
        });
        if (_projectInfo != null)
        {
            return markdownSummaryFormatter.Format(_projectInfo);
        }

        if (_solutionInfo != null) return markdownSummaryFormatter.Format(_solutionInfo);
        return string.Empty;
    }


    private async Task<string> BuildFocusedContextAsync()
    {
        if (_focusedContext is null) return string.Empty;

        var variables = new Dictionary<string, object?>
        {
            ["filePath"] = _focusedContext.FilePath,
            //todo: add doc
        };

        return await PromptTemplateRenderer.RenderAsync(
            ContextPromptTemplates.FocusedContextTemplate,
            variables);
    }

    private async Task<string> BuildRelevantSnippetsAsync()
    {
        if (_relevantSnippets.Count == 0) return string.Empty;

        var snippetStrings = new List<string>();
        foreach (var snippet in _relevantSnippets.Take(5)) // 限制数量
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
            ["searchQuery"] = _relevantSnippets.FirstOrDefault()?.Query ?? "user query",
            ["snippetsList"] = allSnippets
        };

        return await PromptTemplateRenderer.RenderAsync(
            ContextPromptTemplates.RelevantSnippetsTemplate,
            mainVariables);
    }
}

/// <summary>
/// 当前聚焦的上下文（用户正在编辑的位置）
/// </summary>
public class FocusedContext
{
    public required string FilePath { get; init; }

    public required DocumentAnalysisResult DocumentAnalysis { get; set; }
}

/// <summary>
/// RAG 检索到的相关代码片段
/// </summary>
public class RelevantSnippet
{
    public required string SourcePath { get; init; }
    public required string Signature { get; init; }
    public string? Summary { get; init; }
    public required string CodeContent { get; init; }
    public required string Query { get; init; }
    public double RelevanceScore { get; init; }
}