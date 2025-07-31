using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

namespace LLMClient.UI.MCP;

public class CheckableFunctionGroupTree : SelectableViewModel<IAIFunctionGroup>, IAIFunctionGroup
{
    public List<VirtualFunctionViewModel> Functions { get; set; } = new List<VirtualFunctionViewModel>();

    public CheckableFunctionGroupTree(IAIFunctionGroup data) : base(data)
    {
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
            return Functions
                .Where(function => function is { IsSelected: true, IsEnabled: true })
                .Select(function => function.Function!)
                .ToArray();
        }
    }

    public string GetUniqueId()
    {
        throw new NotSupportedException();
    }

    public virtual async Task EnsureAsync(CancellationToken cancellationToken)
    {
        await Data.EnsureAsync(cancellationToken);
        foreach (var unSelected in this.Functions.Where(model => !model.IsSelected).ToArray())
        {
            this.Functions.Remove(unSelected);
        }

        foreach (var function in this.Functions)
        {
            function.Function = null;
        }

        var availableTools = Data.AvailableTools;
        if (availableTools != null)
        {
            foreach (var availableTool in availableTools)
            {
                var firstOrDefault = this.Functions.FirstOrDefault(model => model.IsMatch(availableTool));
                if (firstOrDefault != null)
                {
                    firstOrDefault.Function = availableTool;
                }
                else
                {
                    this.Functions.Add(new VirtualFunctionViewModel(availableTool));
                }
            }
        }

        OnPropertyChangedAsync(nameof(Functions));
        OnPropertyChangedAsync(nameof(AvailableTools));
    }

    public object Clone()
    {
        throw new NotSupportedException();
    }
}