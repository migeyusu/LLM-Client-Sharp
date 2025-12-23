using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.ContextEngineering;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using Microsoft.Extensions.Logging;

namespace LLMClient.Project;

public class CSharpProjectViewModel : ProjectViewModel, IDisposable
{
    private string? _projectFilePath;

    public string? ProjectFilePath
    {
        get => _projectFilePath;
        set
        {
            if (value == _projectFilePath) return;
            _projectFilePath = value;
            OnPropertyChanged();
        }
    }

    private string? _solutionFilePath;

    public string? SolutionFilePath
    {
        get => _solutionFilePath;
        set
        {
            if (value == _solutionFilePath) return;
            _solutionFilePath = value;
            OnPropertyChanged();
        }
    }

    public bool IsSolutionMode { get; set; } = false;

    private readonly RoslynProjectAnalyzer _analyzer;

    public CSharpProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, ILogger<RoslynProjectAnalyzer> logger,
        IRagSourceCollection ragSourceCollection, IEnumerable<ProjectTaskViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, ragSourceCollection, tasks)
    {
        _analyzer = new RoslynProjectAnalyzer(logger, new AnalyzerConfig()
        {
            IncludeTestProjects = true,
            IncludePrivateMembers = true,
            MaxConcurrency = 4
        });
    }

    private async Task<string> GetProjectSummary()
    {
        var markdownSummaryFormatter = new MarkdownSummaryFormatter(new FormatterOptions()
        {
            IncludeMembers = true,
            IncludePackages = true
        });
        if (IsSolutionMode)
        {
            if (string.IsNullOrEmpty(SolutionFilePath))
            {
                throw new NotSupportedException("Solution file path cannot be null or empty.");
            }

            var solutionInfo = await _analyzer.AnalyzeSolutionAsync(SolutionFilePath);
            return markdownSummaryFormatter.Format(solutionInfo);
        }
        else
        {
            if (string.IsNullOrEmpty(ProjectFilePath))
            {
                throw new NotSupportedException("Project file path cannot be null or empty.");
            }

            var projectInfo = await _analyzer.AnalyzeProjectAsync(ProjectFilePath);
            return markdownSummaryFormatter.Format(projectInfo);
        }
    }

    protected override async Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2,
        int? index = null)
    {
        var projectSummary = await GetProjectSummary();
        
        return await base.GetResponse(arg1, arg2, index);
    }

    public void Dispose()
    {
        _analyzer.Dispose();
    }
}