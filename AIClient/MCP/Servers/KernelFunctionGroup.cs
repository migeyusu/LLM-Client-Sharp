using System.Text.Json.Serialization;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace LLMClient.MCP.Servers;

public abstract class KernelFunctionGroup : BaseViewModel, IBuiltInFunctionGroup
{
    public abstract string? AdditionPrompt { get; }

    private IReadOnlyList<AIFunction>? _availableTools;
    [JsonIgnore]
    public IReadOnlyList<AIFunction>? AvailableTools
    {
        get => _availableTools;
        set
        {
            if (Equals(value, _availableTools)) return;
            _availableTools = value;
            OnPropertyChanged();
        }
    }

    public virtual bool IsAvailable { get; } = true;

    public bool IsToolAvailable { get; } = true;
    
    [JsonIgnore] public string Name { get; }

    public KernelFunctionGroup(string pluginName)
    {
        if (string.IsNullOrEmpty(pluginName.Trim()))
        {
            throw new ArgumentException($"{nameof(pluginName)} cannot be null or empty.");
        }

        Name = pluginName;
        var kernelPlugin = KernelPluginFactory.CreateFromObject(this, pluginName);
#pragma warning disable SKEXP0001
        this.AvailableTools = kernelPlugin.AsAIFunctions().ToArray();
#pragma warning restore SKEXP0001
    }

    public string GetUniqueId()
    {
        return $"{this.GetType().FullName}";
    }

    public Task EnsureAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public abstract object Clone();
}