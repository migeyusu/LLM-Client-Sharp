using LLMClient.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.UI.MCP;

public class AIFunctionSelectionViewModel : BaseViewModel
{
    public IReadOnlyList<SelectableViewModel<IAIFunctionGroup>> FunctionCollection { get; }

    public IMcpServiceCollection McpServiceCollection
    {
        get { return ServiceLocator.GetService<IMcpServiceCollection>()!; }
    }

    public IBuiltInFunctionsCollection BuiltInFunctionsCollection
    {
        get { return ServiceLocator.GetService<IBuiltInFunctionsCollection>()!; }
    }

    public AIFunctionSelectionViewModel(IEnumerable<IAIFunctionGroup> selectedGroups, bool enableBuiltInFunctions)
    {
        var list = new List<SelectableViewModel<IAIFunctionGroup>>();
        list.AddRange(this.McpServiceCollection.Select((group => new SelectableViewModel<IAIFunctionGroup>(group))));
        if (enableBuiltInFunctions)
        {
            list.AddRange(this.BuiltInFunctionsCollection.Select((function) =>
                new SelectableViewModel<IAIFunctionGroup>(function)));
        }

        foreach (var group in selectedGroups)
        {
            SelectableViewModel<IAIFunctionGroup>? selectable = null;
            if ((selectable = list.Find(model => AIFunctionGroupComparer.Instance.Equals(group, model.Data))) != null)
            {
                selectable.IsSelected = true;
            }
            else
            {
                list.Add(new SelectableViewModel<IAIFunctionGroup>(group) { IsSelected = true });
            }
        }

        FunctionCollection = list;
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