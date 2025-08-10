using Microsoft.Extensions.AI;

namespace LLMClient.UI.MCP;

/// <summary>
/// 虚加载的函数视图模型
/// </summary>
public class VirtualFunctionViewModel : BaseViewModel, ICloneable
{
    private readonly CheckableFunctionGroupTree _parentNode;

    private bool _isSelected;
    private string? _functionName;
    private string? _description;
    private bool _isEnabled;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected) return;
            _isSelected = value;
            OnPropertyChanged();
            _parentNode?.RefreshCheckState();
        }
    }

    public void ApplyFunction(AIFunction? value)
    {
        if (value == null)
        {
            IsEnabled = false;
        }
        else
        {
            FunctionName = value.Name;
            Description = value.Description;
            IsEnabled = true;
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value == _isEnabled) return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (value == _description) return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public string? FunctionName
    {
        get => _functionName;
        set
        {
            if (value == _functionName) return;
            _functionName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEnabled));
        }
    }

    public bool IsMatch(AIFunction function)
    {
        return this.FunctionName == function.Name;
    }

    public VirtualFunctionViewModel(AIFunction function, CheckableFunctionGroupTree parentNode)
    {
        this._parentNode = parentNode;
        this.ApplyFunction(function);
    }

    public VirtualFunctionViewModel(string? functionName, CheckableFunctionGroupTree parentNode)
    {
        this.FunctionName = functionName;
        this._parentNode = parentNode;
    }


    public object Clone()
    {
        return new VirtualFunctionViewModel(this.FunctionName, this._parentNode)
        {
            IsSelected = this.IsSelected,
            Description = this.Description,
            IsEnabled = this.IsEnabled
        };
    }
}