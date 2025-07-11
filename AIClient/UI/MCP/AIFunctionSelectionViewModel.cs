using LLMClient.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.UI.MCP;

public class AIFunctionSelectionViewModel : BaseViewModel
{
    public IReadOnlyList<SelectableFunctionGroupViewModel> CandidateFunctions { get; }

    public IReadOnlyList<SelectableFunctionGroupViewModel> McpServices { get; set; }

    public IReadOnlyList<SelectableFunctionGroupViewModel> BuiltInFunctions { get; set; }

    private SelectableFunctionGroupViewModel? _focusedFunction;

    public SelectableFunctionGroupViewModel? FocusedFunction
    {
        get => _focusedFunction;
        set
        {
            if (Equals(value, _focusedFunction)) return;
            _focusedFunction = value;
            OnPropertyChanged();
        }
    }

    public AIFunctionSelectionViewModel(IEnumerable<IAIFunctionGroup> selectedGroups, bool enableBuiltInFunctions)
    {
        var mcpServiceCollection = ServiceLocator.GetService<IMcpServiceCollection>()!;
        var builtInService = ServiceLocator.GetService<IBuiltInFunctionsCollection>()!;
        McpServices = mcpServiceCollection.Select((group => new SelectableFunctionGroupViewModel(group)))
            .ToArray();
        BuiltInFunctions = builtInService.Select((function) =>
            new SelectableFunctionGroupViewModel(function)).ToArray();
        var list = new List<SelectableFunctionGroupViewModel>();
        list.AddRange(McpServices);
        if (enableBuiltInFunctions)
        {
            list.AddRange(BuiltInFunctions);
        }

        foreach (var group in selectedGroups)
        {
            SelectableFunctionGroupViewModel? selectable = null;
            if ((selectable = list.Find(model => AIFunctionGroupComparer.Instance.Equals(group, model.Data))) != null)
            {
                selectable.IsSelected = true;
            }
            else
            {
                list.Add(new SelectableFunctionGroupViewModel(group) { IsSelected = true });
            }
        }

        CandidateFunctions = list;
    }

    public async Task EnsureAsync()
    {
        foreach (var aiFunctionGroup in this.CandidateFunctions)
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