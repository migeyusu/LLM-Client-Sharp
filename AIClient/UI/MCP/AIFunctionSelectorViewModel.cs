using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using LLMClient.UI.MCP.Servers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.MCP;

public class AIFunctionSelectorViewModel : BaseViewModel
{
    private readonly Action? _afterSelect;

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

    public SuspendableObservableCollection<SelectableFunctionGroupViewModel> CandidateFunctions
    {
        get { return _candidateFunctions; }
    }

    private readonly SuspendableObservableCollection<SelectableFunctionGroupViewModel> _candidateFunctions =
        new SuspendableObservableCollection<SelectableFunctionGroupViewModel>();

    private IReadOnlyList<SelectableFunctionGroupViewModel> _mcpServices = [];
    
    private IReadOnlyList<SelectableFunctionGroupViewModel> _builtInFunctions = [];

    public IAIFunctionGroup[] SelectedFunctions
    {
        get { return CandidateFunctions.Where(model => model.IsSelected).Select(model => model.Data).ToArray(); }
        set => UpdateCandidateFunctions(value, true);
    }

    public IReadOnlyList<SelectableFunctionGroupViewModel> McpServices
    {
        get => _mcpServices;
        set
        {
            if (Equals(value, _mcpServices)) return;
            _mcpServices = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<SelectableFunctionGroupViewModel> BuiltInFunctions
    {
        get => _builtInFunctions;
        set
        {
            if (Equals(value, _builtInFunctions)) return;
            _builtInFunctions = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectCommand => new ActionCommand((o) =>
    {
        OnPropertyChanged(nameof(SelectedFunctions));
        _afterSelect?.Invoke();
    });

    public AIFunctionSelectorViewModel(Action? afterSelect = null)
    {
        _afterSelect = afterSelect;
    }

    private void UpdateCandidateFunctions(IEnumerable<IAIFunctionGroup> functionGroups, bool select)
    {
        _candidateFunctions.BeginInit();
        foreach (var functionGroup in functionGroups)
        {
            SelectableFunctionGroupViewModel? selectable = null;
            if ((selectable = _candidateFunctions.FirstOrDefault(model =>
                    AIFunctionGroupComparer.Instance.Equals(functionGroup, model.Data))) != null)
            {
                selectable.IsSelected = true;
            }
            else
            {
                _candidateFunctions.Add(new SelectableFunctionGroupViewModel(functionGroup) { IsSelected = select });
            }
        }

        _candidateFunctions.EndInit();
    }

    private readonly List<IFunctionGroupSource> _sources = new List<IFunctionGroupSource>();

    public AIFunctionSelectorViewModel ConnectSource(IFunctionGroupSource? source)
    {
        if (source == null || _sources.Contains(source))
        {
            return this;
        }

        _sources.Add(source);
        return this;
    }

    public AIFunctionSelectorViewModel ConnectDefault()
    {
        return ConnectSource(ServiceLocator.GetService<IMcpServiceCollection>() as McpServiceCollection)
            .ConnectSource(ServiceLocator.GetService<IBuiltInFunctionsCollection>() as BuiltInFunctionsCollection);
    }

    public AIFunctionSelectorViewModel ResetSource()
    {
        var unselected = _candidateFunctions.Where(model => !model.IsSelected).ToArray();
        foreach (var selectableFunctionGroupViewModel in unselected)
        {
            _candidateFunctions.Remove(selectableFunctionGroupViewModel);
        }

        foreach (var source in _sources)
        {
            UpdateCandidateFunctions(source.GetFunctionGroups(), false);
        }

        McpServices = _candidateFunctions.Where((model => model.Data is McpServerItem)).ToArray();
        BuiltInFunctions = _candidateFunctions.Where((model => model.Data is IBuiltInFunctionGroup)).ToArray();
        return this;
    }

    public async void EnsureAsync()
    {
        var aiFunctionGroups = this.CandidateFunctions.ToArray();
        foreach (var aiFunctionGroup in aiFunctionGroups)
        {
            await aiFunctionGroup.Data.EnsureAsync(CancellationToken.None);
        }
    }
}

public class SelectableFunctionGroupViewModel : SelectableViewModel<IAIFunctionGroup>
{
    public SelectableFunctionGroupViewModel(IAIFunctionGroup data) : base(data)
    {
    }
}

public class SelectableViewModel<T> : BaseViewModel
{
    private bool _isSelected;

    public SelectableViewModel(T data)
    {
        Data = data;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public T Data { get; set; }
}
