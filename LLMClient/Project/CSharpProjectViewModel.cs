using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Data;
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
    private readonly ProjectAwarenessPlugin _projectAwarenessPlugin;
    private readonly SymbolSemanticPlugin _symbolSemanticPlugin;
    private readonly CodeSearchPlugin _codeSearchPlugin;
    private readonly CodeReadingPlugin _codeReadingPlugin;
    private readonly IAIFunctionGroup[] _projectFunctions;

    public override ContextPromptViewModel ProjectContext => _projectContextPrompt;

    private readonly SolutionContext _solutionContext;

    public CSharpProjectViewModel(ProjectOption option, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper,
        ILoggerFactory loggerFactory,
        GlobalOptions options, ITokensCounter tokensCounter, RoslynProjectAnalyzer projectAnalyzer,
        IViewModelFactory factory, IEnumerable<ProjectSessionViewModel>? tasks = null)
        : base(option, initialPrompt, modelClient, mapper, options, factory, tasks)
    {
        _solutionContext = new SolutionContext(projectAnalyzer);
        _projectAwarenessPlugin = new ProjectAwarenessPlugin(new ProjectAwarenessService(_solutionContext));
        _symbolSemanticPlugin = new SymbolSemanticPlugin(new SymbolSemanticService(_solutionContext, mapper,
            loggerFactory.CreateLogger<SymbolSemanticService>()));
        _codeSearchPlugin = new CodeSearchPlugin(new CodeSearchService(_solutionContext, null,
            loggerFactory.CreateLogger<CodeSearchService>()));
        _codeReadingPlugin = new CodeReadingPlugin(new CodeReadingService(_solutionContext, mapper,
            loggerFactory.CreateLogger<CodeReadingService>()));
        _projectFunctions =
        [
            _projectAwarenessPlugin,
            _symbolSemanticPlugin,
            _codeSearchPlugin,
            _codeReadingPlugin
        ];
        Requester.FunctionTreeSelector.ConnectSource(new ProxyFunctionGroupSource(() => _projectFunctions));
        _projectContextPrompt = new CSharpContextPromptViewModel(_solutionContext, this);
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
        if (string.IsNullOrEmpty(SolutionFilePath))
        {
            throw new InvalidOperationException("Please select a solution file before sending the request.");
        }

        if (_solutionContext.IsLoaded)
        {
            await _solutionContext.Analyzer.AnalysisCurrentSolutionAsync(token);
        }
        else
        {
            await _solutionContext.LoadSolutionAsync(SolutionFilePath, token);
        }

        await base.PreviewProcessing(token);
    }

    public override bool TryResolvePersistedFunctionGroup(AIFunctionGroupDefinitionPersistModel persistModel,
        out IAIFunctionGroup? functionGroup)
    {
        functionGroup = persistModel switch
        {
            ProjectAwarenessPluginPersistModel => _projectAwarenessPlugin,
            SymbolSemanticPluginPersistModel => _symbolSemanticPlugin,
            CodeSearchPluginPersistModel => _codeSearchPlugin,
            CodeReadingPluginPersistModel => _codeReadingPlugin,
            _ => null
        };
        return functionGroup != null;
    }

    public void Dispose()
    {
        _projectContextPrompt.Dispose();
    }
}