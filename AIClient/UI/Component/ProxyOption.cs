using System.Net;
using System.Net.Http;
using Google.Apis.Http;
using IHttpClientFactory = Google.Apis.Http.IHttpClientFactory;

namespace LLMClient.UI.Component;

public enum ProxyType:int
{
    Default = 0,
    System = 1,
    Custom = 2,
    Direct = 3,
}

public class ProxyOption : BaseViewModel<ProxyOption>
{
    private ProxyType _proxyType;

    public ProxyType ProxyType
    {
        get => _proxyType;
        set
        {
            if (value == _proxyType) return;
            _proxyType = value;
            OnPropertyChanged();
        }
    }

    private string? _proxyString;

    public string? ProxyString
    {
        get => _proxyString;
        set
        {
            if (value == _proxyString) return;
            _proxyString = value;
            OnPropertyChanged();
        }
    }

    public HttpClientHandler CreateHandler()
    {
        switch (ProxyType)
        {
            case ProxyType.Default:
                return new HttpClientHandler();
            case ProxyType.System:
                return new HttpClientHandler()
                {
                    Proxy = DynamicProxy.Instance,
                    UseProxy = true,
                };
            case ProxyType.Custom:
                if (string.IsNullOrEmpty(ProxyString))
                {
                    return new HttpClientHandler();
                }

                return new HttpClientHandler()
                {
                    UseProxy = true,
                    Proxy = new WebProxy(new Uri(ProxyString))
                };
            case ProxyType.Direct:
                return new HttpClientHandler() { UseProxy = false };
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected bool Equals(ProxyOption other)
    {
        return _proxyType == other._proxyType && _proxyString == other._proxyString;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ProxyOption)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.ProxyType, this.ProxyString);
    }

    public IHttpClientFactory CreateFactory()
    {
        return new ProxyOptionFactory(this);
    }

    private class ProxyOptionFactory : HttpClientFactory
    {
        private readonly ProxyOption _option;

        public ProxyOptionFactory(ProxyOption option)
        {
            _option = option;
        }

        protected override HttpClientHandler CreateClientHandler()
        {
            return _option.CreateHandler();
        }
    }
}