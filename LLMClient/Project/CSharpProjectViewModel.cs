using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering;
using LLMClient.Dialog;
using LLMClient.Endpoints;

namespace LLMClient.Project;

public class CppProjectViewModel : ProjectViewModel
{
    public CppProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory,
        IEnumerable<ProjectTaskViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, factory, tasks)
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

    private readonly CSharpContextPromptViewModel _projectContextPrompt;

    public override ContextPromptViewModel ProjectContextPrompt => _projectContextPrompt;

    public CSharpProjectViewModel(ProjectOption option, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, ITokensCounter tokensCounter,
        IViewModelFactory factory, IEnumerable<ProjectTaskViewModel>? tasks = null)
        : base(option, modelClient, mapper, options, factory, tasks)
    {
        _projectContextPrompt = new CSharpContextPromptViewModel(this, tokensCounter);
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

    }

    protected override async Task<CompletedResult> GetResponse(ILLMChatClient arg1, IRequestItem arg2,
        int? index = null, CancellationToken token = default)
    {
        return await base.GetResponse(arg1, arg2, index, token);
    }

    public void Dispose()
    {
        _projectContextPrompt.Dispose();
    }
}