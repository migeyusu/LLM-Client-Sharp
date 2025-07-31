using System.Collections.ObjectModel;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.MCP.Servers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.MCP;

public class AIFunctionTreeSelectorViewModel : BaseViewModel
{
    private bool _functionEnabled;

    public bool FunctionEnabled
    {
        get => _functionEnabled;
        set
        {
            if (value == _functionEnabled) return;
            _functionEnabled = value;
            OnPropertyChanged();
        }
    }
    
    private IReadOnlyList<CheckableFunctionGroupTree> _mcpServices = [];

    public IReadOnlyList<CheckableFunctionGroupTree> McpServices
    {
        get => _mcpServices;
        set
        {
            if (Equals(value, _mcpServices)) return;
            _mcpServices = value;
            OnPropertyChanged();
        }
    }

        
    private IReadOnlyList<CheckableFunctionGroupTree> _builtInFunctions = [];

    public IReadOnlyList<CheckableFunctionGroupTree> BuiltInFunctions
    {
        get => _builtInFunctions;
        set
        {
            if (Equals(value, _builtInFunctions)) return;
            _builtInFunctions = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CheckableFunctionGroupTree> FunctionGroups { get; } = [];

    public ICommand SelectCommand => new ActionCommand((o) => { AfterSelect?.Invoke(); });

    public IEnumerable<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get
        {
            return FunctionGroups.Where(tree =>
                tree.IsSelected && (tree.Functions?.Any(model => model.IsSelected)) == true);
        }
    }

    public event Action? AfterSelect;

    public AIFunctionTreeSelectorViewModel()
    {
    }

    private readonly List<IFunctionGroupSource> _sources = new List<IFunctionGroupSource>();

    public AIFunctionTreeSelectorViewModel ConnectSource(IFunctionGroupSource? source)
    {
        if (source == null || _sources.Contains(source))
        {
            return this;
        }

        _sources.Add(source);
        return this;
    }

    public AIFunctionTreeSelectorViewModel ConnectDefault()
    {
        return ConnectSource(ServiceLocator.GetService<IMcpServiceCollection>() as McpServiceCollection)
            .ConnectSource(ServiceLocator.GetService<IBuiltInFunctionsCollection>() as BuiltInFunctionsCollection);
    }

    /// <summary>
    /// 可以使用<see cref="AIFunctionTreeSelectorViewModel"/>或普通的<see cref="IAIFunctionGroup"/>作为参数
    /// </summary>
    /// <param name="functionGroups"></param>
    /// <param name="select"></param>
    private void UpdateCandidateFunctions(IEnumerable<IAIFunctionGroup> functionGroups, bool select)
    {
        foreach (var functionGroup in functionGroups)
        {
            var oldTree = FunctionGroups.FirstOrDefault(model =>
                AIFunctionGroupComparer.Instance.Equals(functionGroup, model.Data));
            if (functionGroup is CheckableFunctionGroupTree newTree)
            {
                if (oldTree != null)
                {
                    FunctionGroups.Remove(oldTree);
                }

                FunctionGroups.Add(newTree);
            }
            else
            {
                if (oldTree == null)
                {
                    FunctionGroups.Add(new CheckableFunctionGroupTree(functionGroup)
                    {
                        IsSelected = select
                    });
                }
                else
                {
                    oldTree.IsSelected = select;
                }
            }
        }
    }

    public void ResetItemSource()
    {
        FunctionGroups.Clear();
        foreach (var source in _sources)
        {
            UpdateCandidateFunctions(source.GetFunctionGroups(), false);
        }
        McpServices = FunctionGroups.Where((model => model.Data is McpServerItem)).ToArray();
        BuiltInFunctions = FunctionGroups.Where((model => model.Data is IBuiltInFunctionGroup)).ToArray();
    }

    public async Task EnsureAsync()
    {
        foreach (var aiFunctionGroup in this.FunctionGroups)
        {
            await aiFunctionGroup.EnsureAsync(CancellationToken.None);
        }
    }
}