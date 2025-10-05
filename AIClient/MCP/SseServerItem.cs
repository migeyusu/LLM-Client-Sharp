using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace LLMClient.MCP;

public class SseServerItem : McpServerItem
{
    private string? _url;
    private HttpTransportMode _transportMode = HttpTransportMode.AutoDetect;
    private IDictionary<string, string>? _additionalHeaders;
    private bool _useGlobalProxy = true;
    public override string Type => "sse";

    public override bool Validate()
    {
        if (string.IsNullOrEmpty(this.Url))
        {
            return false;
        }

        return true;
    }

    public string? Url
    {
        get => _url;
        set
        {
            if (value == _url) return;
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Url cannot be null or empty.");
            }

            _url = value;
            OnPropertyChanged();
        }
    }

    public HttpTransportMode TransportMode
    {
        get => _transportMode;
        set
        {
            if (value == _transportMode) return;
            _transportMode = value;
            OnPropertyChanged();
        }
    }

    public IDictionary<string, string>? AdditionalHeaders
    {
        get => _additionalHeaders;
        set
        {
            if (Equals(value, _additionalHeaders)) return;
            _additionalHeaders = value;
            OnPropertyChanged();
        }
    }

    public bool UseGlobalProxy
    {
        get => _useGlobalProxy;
        set
        {
            if (value == _useGlobalProxy) return;
            _useGlobalProxy = value;
            OnPropertyChanged();
        }
    }

    public ProxyOption ProxyOption { get; set; } = new ProxyOption();

    public ICommand ConfigHeadersCommand => new RelayCommand(() =>
    {
        var envWindow = new KeyValueConfigWindow()
        {
            Title = "Http Headers",
            UserVariables = new ObservableCollection<VariableItem>(this.AdditionalHeaders?.Select(item =>
                new VariableItem()
                {
                    Name = item.Key,
                    Value = item.Value
                }) ?? []),
        };
        if (envWindow.ShowDialog() == true)
        {
            var userVariables = envWindow.UserVariables;
            if (userVariables.Any())
            {
                this.AdditionalHeaders = userVariables.ToDictionary(
                    item => item.Name!,
                    item => item.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                this.AdditionalHeaders = null;
            }
        }
    });

    public override string GetUniqueId()
    {
        return $"sse:{Name},{Url}";
    }

    protected override IClientTransport GetTransport()
    {
        if (string.IsNullOrEmpty(this.Url))
        {
            throw new NotSupportedException("Url cannot be null or empty.");
        }

        var proxyOption = UseGlobalProxy
            ? ServiceLocator.GetService<GlobalOptions>()!.ProxyOption
            : ProxyOption;
        var sseClientTransportOptions = new SseClientTransportOptions
        {
            Name = this.Name,
            Endpoint = new Uri(this.Url),
            TransportMode = TransportMode,
            AdditionalHeaders = this.AdditionalHeaders,
        };
        return new SseClientTransport(sseClientTransportOptions, new HttpClient(ProxyOption.CreateHandler()));
    }
}