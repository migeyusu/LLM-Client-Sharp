using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.AvalonEdit.CodeCompletion;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.ContextEngineering;
using LLMClient.Dialog;
using LLMClient.Endpoints;
using Microsoft.Extensions.Logging;

namespace LLMClient.Project;

public class CppProjectViewModel : ProjectViewModel
{
    public CppProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IRagSourceCollection ragSourceCollection,
        IEnumerable<ProjectTaskViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, ragSourceCollection, tasks)
    {
    }
}

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

    private bool _isSolutionMode = false;

    public bool IsSolutionMode
    {
        get => _isSolutionMode;
        set
        {
            if (value == _isSolutionMode) return;
            _isSolutionMode = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectPathCommand { get; }

    private readonly RoslynProjectAnalyzer _analyzer;

    public CSharpProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, ILogger<RoslynProjectAnalyzer> logger,
        IRagSourceCollection ragSourceCollection, IEnumerable<ProjectTaskViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, ragSourceCollection, tasks)
    {
        SelectPathCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = IsSolutionMode ? "Solution Files|*.sln" : "C# Project Files|*.csproj"
            };
            var result = dialog.ShowDialog();
            if (result == true)
            {
                if (IsSolutionMode)
                {
                    SolutionFilePath = dialog.FileName;
                }
                else
                {
                    ProjectFilePath = dialog.FileName;
                }
            }
        });
        _analyzer = new RoslynProjectAnalyzer(logger, new AnalyzerConfig()
        {
            IncludeTestProjects = true,
            IncludePrivateMembers = true,
            MaxConcurrency = 4
        });
    }

    /// <summary>
    /// 由于project/solution生成有明显延迟，所以不绑定到Context属性，使用特殊方法获取，UI也需要特殊触发
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    protected override async Task<string> GetProjectContext()
    {
        var contextPromptBuilder = new ContextPromptBuilder();
        if (IsSolutionMode)
        {
            if (string.IsNullOrEmpty(SolutionFilePath))
            {
                throw new NotSupportedException("Solution file path cannot be null or empty.");
            }

            var solutionInfo = await _analyzer.AnalyzeSolutionAsync(SolutionFilePath);
            contextPromptBuilder.WithSolutionInfo(solutionInfo);
        }
        else
        {
            if (string.IsNullOrEmpty(ProjectFilePath))
            {
                throw new NotSupportedException("Project file path cannot be null or empty.");
            }

            var projectInfo = await _analyzer.AnalyzeProjectAsync(ProjectFilePath);
            contextPromptBuilder.WithProjectInfo(projectInfo);
        }

        return await contextPromptBuilder.BuildAsync();
    }

    protected override async Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2,
        int? index = null)
    {
        this.ProjectContext = await GetProjectContext();
        return await base.GetResponse(arg1, arg2, index);
    }

    public void Dispose()
    {
        _analyzer.Dispose();
    }
}