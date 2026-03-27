using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.ToolCall;

/// <summary>
/// 虚加载的函数视图模型
/// </summary>
public class VirtualFunctionViewModel : BaseViewModel, ICloneable
{
    private readonly CheckableFunctionGroupTree _parentNode;

    public bool IsSelected
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Description
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? FunctionName
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
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