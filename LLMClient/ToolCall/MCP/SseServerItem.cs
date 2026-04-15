using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.UserControls;
using LLMClient.Configuration;
using ModelContextProtocol.Client;

namespace LLMClient.ToolCall.MCP;

public class SseServerItem : McpServerItem
{
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
        get;
        set
        {
            if (value == field) return;
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Url cannot be null or empty.");
            }

            field = value;
            OnPropertyChanged();
        }
    }

    public HttpTransportMode TransportMode
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = HttpTransportMode.AutoDetect;

    public bool BufferedRequest
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool RemoveCharSet
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IDictionary<string, string>? AdditionalHeaders
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ProxySetting ProxySetting { get; set; } = new ProxySetting();

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

        var sseClientTransportOptions = new HttpClientTransportOptions()
        {
            Name = this.Name,
            Endpoint = new Uri(this.Url),
            TransportMode = TransportMode,
            AdditionalHeaders = this.AdditionalHeaders,
        };
        HttpMessageHandler clientHandler = this.ProxySetting.GetRealProxy().CreateHandler();
        if (BufferedRequest || RemoveCharSet)
        {
            clientHandler = new CustomHttpHandler(clientHandler)
            {
                BufferedRequest = this.BufferedRequest,
                RemoveCharSet = this.RemoveCharSet,
            };
        }

        return new HttpClientTransport(sseClientTransportOptions, new HttpClient(clientHandler),
            ownsHttpClient: true);
    }
}