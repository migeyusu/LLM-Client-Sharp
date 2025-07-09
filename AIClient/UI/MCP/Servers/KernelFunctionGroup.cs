using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace LLMClient.UI.MCP.Servers;

public abstract class KernelFunctionGroup : BaseViewModel, IAIFunctionGroup
{
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

    public bool IsToolAvailable { get; } = true;
    
    

    [JsonIgnore] public string Name { get; }

    private bool _isEnabled;

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


    private IReadOnlyList<AIFunction>? _availableTools;

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

    public object Clone()
    {
        return Activator.CreateInstance(this.GetType())!;
    }
}