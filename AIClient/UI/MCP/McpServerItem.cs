using System.Text.Json;
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
public abstract class McpServerItem : NotifyDataErrorInfoViewModelBase, IAIFunctionGroup, IAsyncDisposable
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

    public ICommand RefreshToolsCommand => new RelayCommand(async () => { await ListToolsAsync(); });

    public ICommand ResetToolsCommand => new RelayCommand(async () => { await ResetToolsAsync(); });

    /// <summary>
    /// 列举支持的操作
    /// </summary>
    /// <returns></returns>
    public async Task<bool> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!Validate())
        {
            return false;
        }

        try
        {
            ErrorMessage = null;
            if (_client == null)
            {
                var transport = this.GetTransport();
                _client = await McpClientFactory.CreateAsync(transport, cancellationToken: cancellationToken);
            }

            this.AvailableTools =
                (await _client.ListToolsAsync(cancellationToken: cancellationToken)).ToArray<AIFunction>();
            return true;
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
            return false;
        }
    }

    /// <summary>
    /// 重置工具列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task ResetToolsAsync(CancellationToken cancellationToken = default)
    {
        AvailableTools = null;
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        await ListToolsAsync(cancellationToken);
    }

    private IMcpClient? _client;

    public async Task<IList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        if (AvailableTools == null)
        {
            await ListToolsAsync(cancellationToken);
        }

        return AvailableTools ?? new List<AIFunction>();
    }

    protected abstract IClientTransport GetTransport();

    public abstract bool Validate();

    public async ValueTask DisposeAsync()
    {
        if (_client != null) await _client.DisposeAsync();
    }

    public object Clone()
    {
        var serialize = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize(serialize, this.GetType())!;
    }
}