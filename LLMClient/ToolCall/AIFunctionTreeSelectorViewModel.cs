using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using LLMClient.ToolCall.DefaultPlugins;
using LLMClient.ToolCall.MCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.ToolCall;

public class AIFunctionTreeSelectorViewModel : BaseViewModel
{
    public bool IsFunctionEnabled
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IList<FunctionCallEngineType> SelectableCallEngineTypes
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public FunctionCallEngineType? EngineType
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CheckableFunctionGroupTree> FunctionGroups { get; } = [];

    public ICommand SelectCommand { get; }

    public ICommand RefreshSourceCommand { get; }

    public ICommand CancelRefreshCommand { get; }

    public IEnumerable<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get { return FunctionGroups.Where(tree => tree.IsSelected != false); }
    }

    public event Action? AfterSelect;

    public AIFunctionTreeSelectorViewModel()
    {
        SelectCommand = new ActionCommand((o) => { AfterSelect?.Invoke(); });
        RefreshSourceCommand = new ActionCommand(async (o) => { await RefreshSourceAsync(); });
        CancelRefreshCommand = new ActionCommand((o) => { _refreshSource?.Cancel(); });
    }

    private readonly List<IFunctionGroupSource> _sources = [];

    public AIFunctionTreeSelectorViewModel ConnectSource(IFunctionGroupSource? source)
    {
        if (source == null || _sources.Contains(source))
        {
            return this;
        }

        _sources.Add(source);
        return this;
    }

    public void ClearSource()
    {
        _sources.Clear();
    }

    public AIFunctionTreeSelectorViewModel ConnectDefault()
    {
        return ConnectSource(ServiceLocator.GetService<IMcpServiceCollection>() as McpServiceCollection)
            .ConnectSource(ServiceLocator.GetService<BuiltInFunctionsCollection>());
    }


    public bool IsEnsuring
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = false;

    private bool _isInitialized = false;

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
                MessageEventBus.Publish("刷新函数列表失败：" + e.Message);
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