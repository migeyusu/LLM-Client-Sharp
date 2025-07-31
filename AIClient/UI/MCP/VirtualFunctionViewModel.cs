using Microsoft.Extensions.AI;

namespace LLMClient.UI.MCP;

/// <summary>
/// 虚加载的函数视图模型
/// </summary>
public class VirtualFunctionViewModel : BaseViewModel
{
    private bool _isSelected;
    private AIFunction? _function;

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

    public AIFunction? Function
    {
        get => _function;
        set
        {
            if (Equals(value, _function)) return;
            _function = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public bool IsEnabled
    {
        get { return Function != null; }
    }

    public string? FunctionName { get; set; }

    public bool IsMatch(AIFunction function)
    {
        return this.FunctionName == function.Name;
    }

    public VirtualFunctionViewModel(AIFunction function)
    {
        this.FunctionName = function.Name;
        this.Function = function;
    }

    public VirtualFunctionViewModel(string functionName)
    {
        this.FunctionName = functionName;
    }
}