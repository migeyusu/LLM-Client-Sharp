using ModelContextProtocol.Client;

namespace LLMClient.UI.MCP;

public class SseServerItem : McpServerItem
{
    private string? _url;
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
            _url = value;
            OnPropertyChanged();
            ClearError();
            if (string.IsNullOrEmpty(value))
            {
                AddError("Url cannot be null or empty.");
            }
        }
    }

    public override IClientTransport Create()
    {
        if (string.IsNullOrEmpty(this.Url))
        {
            throw new NotSupportedException("Url cannot be null or empty.");
        }

        var sseClientTransportOptions = new SseClientTransportOptions()
        {
            Name = this.Name,
            Endpoint = new Uri(this.Url),
            TransportMode = HttpTransportMode.AutoDetect
        };
        return new SseClientTransport(sseClientTransportOptions);
    }
}