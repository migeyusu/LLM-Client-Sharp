using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools;
using LLMClient.ToolCall;
using Microsoft.Extensions.Logging;

namespace LLMClient.Project;

public class CSharpProjectViewModel : ProjectViewModel, IDisposable
{
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

    public ICommand SelectPathCommand { get; }

    private readonly CSharpContextPromptViewModel _projectContextPrompt;

    public override ContextPromptViewModel ProjectContextPrompt => _projectContextPrompt;

    private readonly SolutionContext _solutionContext;

    public CSharpProjectViewModel(ProjectOption option, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper,
        ILoggerFactory loggerFactory,
        GlobalOptions options, ITokensCounter tokensCounter, RoslynProjectAnalyzer projectAnalyzer,
        IViewModelFactory factory, IEnumerable<ProjectSessionViewModel>? tasks = null)
        : base(option, initialPrompt, modelClient, mapper, options, factory, tasks)
    {
        _solutionContext = new SolutionContext(projectAnalyzer);
        IAIFunctionGroup[] projectFunctions =
        [
            new ProjectAwarenessPlugin(new ProjectAwarenessService(_solutionContext)),
            new SymbolSemanticPlugin(new SymbolSemanticService(_solutionContext, mapper,
                loggerFactory.CreateLogger<SymbolSemanticService>())),
            new CodeSearchPlugin(new CodeSearchService(_solutionContext, null,
                loggerFactory.CreateLogger<CodeSearchService>())),
            new CodeReadingPlugin(new CodeReadingService(_solutionContext, mapper,
                loggerFactory.CreateLogger<CodeReadingService>()))
        ];
        Requester.FunctionTreeSelector.ConnectSource(new ProxyFunctionGroupSource(() => projectFunctions));
        _projectContextPrompt = new CSharpContextPromptViewModel(projectAnalyzer, this, tokensCounter);
        SelectPathCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Solution Files|*.sln",
                DefaultDirectory = this.Option.RootPath,
            };
            var result = dialog.ShowDialog();
            if (result == true)
            {
                SolutionFilePath = dialog.FileName;
            }
        });
    }

    public override async Task PreviewProcessing(CancellationToken token = default)
    {
        await base.PreviewProcessing(token);
        if (string.IsNullOrEmpty(SolutionFilePath))
        {
            throw new InvalidOperationException("Please select a solution file before sending the request.");
        }

        await _solutionContext.LoadSolutionAsync(SolutionFilePath, token);
    }

    public void Dispose()
    {
        _projectContextPrompt.Dispose();
    }
}