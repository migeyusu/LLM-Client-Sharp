using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

namespace LLMClient.UI.MCP.Servers;

public class FileSystemFunctionGroup : BaseViewModel, IAIFunctionGroup
{
    public IList<AIFunction>? AvailableTools
    {
        get => _availableTools;
        set
        {
            if (Equals(value, _availableTools)) return;
            _availableTools = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] public string Name { get; } = "FileSystem";

    private bool _isEnabled;

    [JsonIgnore]
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

    private FileSystemPlugin _plugin;

    private KernelPlugin _kernelPlugin;

    private IList<AIFunction>? _availableTools;
    private string _folderPath;

    [Experimental("SKEXP0001")]
    public FileSystemFunctionGroup(string folderPath)
    {
        _folderPath = folderPath;
        _plugin = new FileSystemPlugin([folderPath]);
        _kernelPlugin = KernelPluginFactory.CreateFromObject(_plugin);
        this.AvailableTools = _kernelPlugin.AsAIFunctions().ToArray();
    }

    public Task<IList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        if (AvailableTools == null)
        {
            return Task.FromResult<IList<AIFunction>>(Array.Empty<AIFunction>());
        }

        return Task.FromResult(AvailableTools);
    }


    [Experimental("SKEXP0001")]
    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (value == _folderPath) return;
            _folderPath = value;
            OnPropertyChanged();
            this._plugin = new FileSystemPlugin(new[] { value });
            this._kernelPlugin = KernelPluginFactory.CreateFromObject(_plugin);
            this.AvailableTools = _kernelPlugin.AsAIFunctions().ToArray();
        }
    }
}