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

    /// <summary>
    /// mcp project url
    /// </summary>
    public Uri? ProjectUrl
    {
        get => _projectUrl;
        set
        {
            if (Equals(value, _projectUrl)) return;
            _projectUrl = value;
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
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Name cannot be null or empty.");
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    private string? _additionPrompt;

    public string? AdditionPrompt
    {
        get => _additionPrompt;
        set
        {
            if (value == _additionPrompt) return;
            _additionPrompt = value;
            OnPropertyChanged();
        }
    }

    private IReadOnlyList<AIFunction>? _availableTools;
    private string? _errorMessage;

    [JsonIgnore]
    public IReadOnlyList<AIFunction>? AvailableTools
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
            MessageEventBus.Publish($"Error refreshing tools {this.Name}: {e.Message}");
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
    private Uri? _projectUrl;

    public abstract string GetUniqueId();

    public Task EnsureAsync(CancellationToken token)
    {
        return ListToolsAsync(token);
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