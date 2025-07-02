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

    [JsonIgnore]
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value == _errorMessage) return;
            _errorMessage = value;
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
    private string? _errorMessage;

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

    public ICommand RefreshCommand => new RelayCommand(async () => { await RefreshOps(); });

    /// <summary>
    /// 列举支持的操作
    /// </summary>
    /// <returns></returns>
    public async Task RefreshOps()
    {
        try
        {
            ErrorMessage = null;
            this.AvailableTools = await SearchToolsAsync();
        }
        catch (Exception e)
        {
            var exception = e;
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            ErrorMessage = exception.Message;
            MessageEventBus.Publish($"Error refreshing tools: {e.Message}");
        }
    }

    public async Task<IList<AIFunction>> SearchToolsAsync(CancellationToken cancellationToken = default)
    {
        var transport = this.Create();
        var client = await McpClientFactory.CreateAsync(transport, cancellationToken: cancellationToken);
        return (await client.ListToolsAsync(cancellationToken: cancellationToken)).ToArray<AIFunction>();
    }


    public abstract IClientTransport Create();

    public abstract bool Validate();
}