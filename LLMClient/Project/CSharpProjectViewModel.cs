using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Agent;
using LLMClient.Agent.Code;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Agent.Planner;
using LLMClient.Agent.Research;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.ContextEngineering.Analysis;
using LLMClient.ContextEngineering.PromptGeneration;
using LLMClient.ContextEngineering.Tools;
using LLMClient.Persistence;
using LLMClient.ToolCall;
using Microsoft.Extensions.Logging;

namespace LLMClient.Project;

public class CSharpProjectViewModel : ProjectViewModel, IDisposable
{
    public string? SolutionFilePath
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectPathCommand { get; }

    private readonly CSharpContextPromptViewModel _projectContextPrompt;
    private readonly ProjectAwarenessPlugin _projectAwarenessPlugin;
    private readonly SymbolSemanticPlugin _symbolSemanticPlugin;
    private readonly CodeSearchPlugin _codeSearchPlugin;
    private readonly CodeReadingPlugin _codeReadingPlugin;
    private readonly CodeMutationPlugin _codeMutationPlugin;
    private readonly IAIFunctionGroup[] _projectTools;

    public override ContextPromptViewModel ProjectContext => _projectContextPrompt;

    private readonly SolutionContext _solutionContext;

    public CSharpProjectViewModel(ProjectOption option, string initialPrompt, ILLMChatClient modelClient,
        IMapper mapper,
        ILoggerFactory loggerFactory,
        GlobalOptions options, RoslynProjectAnalyzer projectAnalyzer,
        IViewModelFactory factory,
        IEnumerable<ProjectSessionViewModel>? tasks = null)
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
        _codeMutationPlugin = new CodeMutationPlugin(new CodeMutationService(_solutionContext,
            loggerFactory.CreateLogger<CodeMutationService>()));
        _projectTools =
        [
            _projectAwarenessPlugin,
            _symbolSemanticPlugin,
            _codeSearchPlugin,
            _codeReadingPlugin,
            _codeMutationPlugin
        ];
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

    public override IAIFunctionGroup[] ProjectTools
    {
        get { return _projectTools; }
    }

    public override async Task PreviewProcessing(CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(SolutionFilePath))
        {
            throw new InvalidOperationException("Please select a solution file before sending the request.");
        }

        var usesProjectFunctions = this.SelectedSession?.SelectedFunctionGroups?.Any(group =>
        {
            return _projectTools.Any(function => function == group.Data);
        }) == true;
        var agentType = Requester.SelectedAgent?.Type;
        if (usesProjectFunctions || (Requester.IsAgentMode && agentType != null &&
                                     _projectAgents.Contains(agentType)))
        {
            if (_solutionContext.IsLoaded)
            {
                await _solutionContext.Analyzer.AnalysisCurrentSolutionAsync(token);
            }
            else
            {
                await _solutionContext.LoadSolutionAsync(SolutionFilePath, token);
            }
        }

        await base.PreviewProcessing(token);
    }

    public override IEnumerable<IAIFunctionGroup> GetInspectorFunctionGroups()
    {
        return _projectTools;
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
            CodeMutationPluginPersistModel => _codeMutationPlugin,
            _ => null
        };
        return functionGroup != null;
    }

    public void Dispose()
    {
        _projectContextPrompt.Dispose();
    }

    private readonly List<Type> _projectAgents =
    [
        typeof(PlannerAgent), typeof(InspectAgent), typeof(CoderAgent)
    ];

    public override IEnumerable<Type> ProjectAgents => _projectAgents;
}