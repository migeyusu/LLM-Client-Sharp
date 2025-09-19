using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.MCP.Servers;
using LLMClient.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.MCP;

public class AIFunctionTreeSelectorViewModel : BaseViewModel
{
    private bool _functionSelected;

    public bool FunctionSelected
    {
        get => _functionSelected;
        set
        {
            if (value == _functionSelected) return;
            _functionSelected = value;
            OnPropertyChanged();
        }
    }

    public IList<FunctionCallEngineType> SelectableCallEngineTypes
    {
        get => _selectableCallEngineTypes;
        set
        {
            if (Equals(value, _selectableCallEngineTypes)) return;
            _selectableCallEngineTypes = value;
            OnPropertyChanged();
        }
    }

    public FunctionCallEngineType? EngineType
    {
        get => _engineType;
        set
        {
            if (value == _engineType) return;
            _engineType = value;
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

    public ICommand RefreshSourceCommand => new ActionCommand(async (o) => { await RefreshSourceAsync(); });

    public ICommand CancelRefreshCommand => new ActionCommand((o) => { _refreshSource?.Cancel(); });

    public IEnumerable<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get { return FunctionGroups.Where(tree => tree.IsSelected != false); }
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

    /*/// <summary>
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
    }*/
    public bool IsEnsuring
    {
        get => _isEnsuring;
        private set
        {
            if (value == _isEnsuring) return;
            _isEnsuring = value;
            OnPropertyChanged();
        }
    }

    private bool _isInitialized = false;
    private bool _isEnsuring = false;
    private FunctionCallEngineType? _engineType;
    private IList<FunctionCallEngineType> _selectableCallEngineTypes = [];

    /// <summary>
    /// 重置状态，表示需要重新初始化
    /// </summary>
    public void Reset()
    {
        _isInitialized = false;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await RefreshSourceAsync();
        _isInitialized = true;
    }

    private CancellationTokenSource? _refreshSource;

    public async Task RefreshSourceAsync()
    {
        if (IsEnsuring)
        {
            return;
        }

        IsEnsuring = true;
        try
        {
            var newFunctionGroups = new List<CheckableFunctionGroupTree>();
            var functionGroupComparer = AIFunctionGroupComparer.Instance;
            foreach (var source in _sources)
            {
                foreach (var newFunctionGroup in source.GetFunctionGroups())
                {
                    var duplicatedTree = newFunctionGroups.FirstOrDefault(model =>
                        functionGroupComparer.Equals(newFunctionGroup, model));
                    if (newFunctionGroup is CheckableFunctionGroupTree checkable)
                    {
                        if (duplicatedTree != null)
                        {
                            newFunctionGroups.Remove(duplicatedTree);
                        }

                        newFunctionGroups.Add(checkable);
                    }
                    else
                    {
                        if (duplicatedTree == null)
                        {
                            newFunctionGroups.Add(new CheckableFunctionGroupTree(newFunctionGroup)
                            {
                                IsSelected = false
                            });
                        }
                    }
                }
            }

            //关键步骤，清空上一次函数
            foreach (var virtualFunctionViewModel in newFunctionGroups.SelectMany(newFunctionGroup =>
                         newFunctionGroup.Functions))
            {
                virtualFunctionViewModel.ApplyFunction(null);
            }

            List<CheckableFunctionGroupTree> functionGroupsToRemove = [];
            foreach (var obsoleteFunctionGroup in FunctionGroups)
            {
                var newFunctionGroup = newFunctionGroups.FirstOrDefault(group =>
                    functionGroupComparer.Equals(group, obsoleteFunctionGroup));
                if (newFunctionGroup != null)
                {
                    obsoleteFunctionGroup.SyncSelect(newFunctionGroup);
                    newFunctionGroups.Remove(newFunctionGroup);
                }
                else
                {
                    functionGroupsToRemove.Add(obsoleteFunctionGroup);
                }
            }

            foreach (var functionGroup in newFunctionGroups)
            {
                FunctionGroups.Add(functionGroup);
            }

            foreach (var functionGroup in functionGroupsToRemove)
            {
                FunctionGroups.Remove(functionGroup);
            }

            McpServices = FunctionGroups.Where((model => model.Data is McpServerItem)).ToArray();
            BuiltInFunctions = FunctionGroups.Where((model => model.Data is IBuiltInFunctionGroup)).ToArray();
            try
            {
                using (_refreshSource = new CancellationTokenSource())
                {
                    await Parallel.ForEachAsync(this.FunctionGroups.ToArray(), _refreshSource.Token,
                        async (aiFunctionGroup, ct) => { await aiFunctionGroup.EnsureAsync(ct); });
                }
            }
            catch (OperationCanceledException)
            {
                //忽略
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to refresh function groups: " + e);
            }

            foreach (var functionGroup in this.FunctionGroups)
            {
                functionGroup.RefreshCheckState();
            }
        }
        finally
        {
            IsEnsuring = false;
        }
    }
}