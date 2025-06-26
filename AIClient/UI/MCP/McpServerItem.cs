using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace LLMClient.UI.MCP;

[JsonDerivedType(typeof(StdIOServerItem), "stdio")]
[JsonDerivedType(typeof(SseServerItem), "sse")]
public abstract class McpServerItem : NotifyDataErrorInfoViewModelBase, IAIFunctionGroup
{
    [JsonIgnore] public abstract string Type { get; }

    private bool _isEnabled = true;

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

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Name cannot be null or empty.");
            }
        }
    }

    private IList<AIFunction>? _availableTools;

    [JsonIgnore]
    public IList<AIFunction>? AvailableTools
    {
        get => _availableTools;
        set
        {
            if (Equals(value, _availableTools)) return;
            _availableTools = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsToolAvailable));
        }
    }

    [JsonIgnore]
    public bool IsToolAvailable
    {
        get => _availableTools?.Count > 0;
    }

    public ICommand RefreshCommand => new RelayCommand(async () =>
    {
        try
        {
            var supportedOps = await this.ListSupportedOps();
            this.AvailableTools = supportedOps.ToArray();
        }
        catch (Exception ex)
        {
            MessageEventBus.Publish($"Error refreshing tools: {ex.Message}");
        }
    });

    /// <summary>
    /// 列举支持的操作
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<AIFunction>> ListSupportedOps()
    {
        var transport = this.Create();
        var client = await McpClientFactory.CreateAsync(transport);
        return (await client.ListToolsAsync());
    }

    public abstract IClientTransport Create();

    public abstract bool Validate();
}