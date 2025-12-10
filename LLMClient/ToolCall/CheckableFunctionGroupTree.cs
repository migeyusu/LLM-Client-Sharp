using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.ToolCall;

public class CheckableFunctionGroupTree : BaseViewModel, IAIFunctionGroup
{
    public IAIFunctionGroup Data { get; }

    private bool _isUpdatingFromParent = false;

    private bool? _isSelected;

    public bool? IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected) return;
            _isSelected = value;
            OnPropertyChanged();
            if (value.HasValue)
            {
                _isUpdatingFromParent = true;
                foreach (var b in Functions)
                {
                    b.IsSelected = value.Value;
                }

                _isUpdatingFromParent = false;
            }
        }
    }

    public SuspendableObservableCollection<VirtualFunctionViewModel> Functions { get; set; } = [];

    public CheckableFunctionGroupTree(IAIFunctionGroup data)
    {
        Data = data;
    }

    public string Name
    {
        get { return Data.Name; }
    }

    public string? AdditionPrompt
    {
        get { return Data.AdditionPrompt; }
    }

    public IReadOnlyList<AIFunction>? AvailableTools
    {
        get
        {
            var availableTools = this.Data.AvailableTools;
            if (availableTools == null || availableTools.Count == 0)
            {
                return [];
            }

            // 过滤掉未选中或不可用的函数
            return availableTools
                .Where(function =>
                {
                    var functionName = function.Name;
                    return this.Functions.Any(model => model.FunctionName == functionName);
                })
                .ToArray();
        }
    }

    public bool IsAvailable => Data.IsAvailable;

    public string GetUniqueId()
    {
        return Data.GetUniqueId();
    }

    public virtual async Task EnsureAsync(CancellationToken cancellationToken)
    {
        await Data.EnsureAsync(cancellationToken);
        await UIThread.SwitchTo();
        var availableTools = Data.AvailableTools?.ToList();
        if (availableTools != null)
        {
            List<VirtualFunctionViewModel> functionsToRemove = [];
            foreach (var functionViewModel in this.Functions)
            {
                var aiFunction = availableTools.FirstOrDefault((function => functionViewModel.IsMatch(function)));
                if (aiFunction != null)
                {
                    functionViewModel.ApplyFunction(aiFunction);
                    availableTools.Remove(aiFunction);
                }
                else
                {
                    functionsToRemove.Add(functionViewModel);
                }
            }

            foreach (var functionViewModel in functionsToRemove)
            {
                if (!functionViewModel.IsSelected)
                {
                    this.Functions.Remove(functionViewModel);
                }
            }

            foreach (var availableTool in availableTools)
            {
                this.Functions.Add(new VirtualFunctionViewModel(availableTool, this));
            }
        }

        OnPropertyChangedAsync(nameof(Functions));
        OnPropertyChangedAsync(nameof(AvailableTools));
    }

    public void RefreshCheckState()
    {
        if (_isUpdatingFromParent) return; // 父节点批量操作时不处理
        if (Functions.Count == 0)
        {
            IsSelected = false;
            return;
        }

        if (Functions.All(b => b.IsSelected))
            this.IsSelected = true;
        else if (Functions.All(b => b.IsSelected == false))
            IsSelected = false;
        else
            IsSelected = null;
    }

    /// <summary>
    /// 只同步状态，不同步函数列表（因为函数可能会过时）。
    /// </summary>
    /// <param name="other"></param>
    public void SyncSelect(CheckableFunctionGroupTree other)
    {
        this.IsSelected = other.IsSelected;
        var otherFunctions = other.Functions.ToList();
        List<VirtualFunctionViewModel> functionsToRemove = [];
        foreach (var function in this.Functions)
        {
            var functionName = function.FunctionName;
            var otherFunction = otherFunctions.FirstOrDefault(model => model.FunctionName == functionName);
            if (otherFunction != null)
            {
                function.IsSelected = otherFunction.IsSelected;
                otherFunctions.Remove(otherFunction);
            }
            else
            {
                functionsToRemove.Add(function);
            }
        }

        foreach (var model in functionsToRemove)
        {
            this.Functions.Remove(model);
        }

        foreach (var otherFunction in otherFunctions)
        {
            this.Functions.Add(otherFunction);
        }
    }

    public object Clone()
    {
        return new CheckableFunctionGroupTree(this.Data)
        {
            IsSelected = this.IsSelected,
            Functions = new SuspendableObservableCollection<VirtualFunctionViewModel>(
                this.Functions.Select(function => (VirtualFunctionViewModel)function.Clone()))
        };
    }
}