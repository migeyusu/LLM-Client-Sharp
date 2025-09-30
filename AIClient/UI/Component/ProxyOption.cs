using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace LLMClient.UI.Component;

public enum ProxyType
{
    Default = 0,
    System = 1,
    Custom = 2,
    Direct = 3,
}

public class ProxyOption : BaseViewModel
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

    public IWebProxy? CreateProxy()
    {
        switch (ProxyType)
        {
            case ProxyType.Default:
                return null;
            case ProxyType.System:
                return new DynamicProxy();
            case ProxyType.Custom:
                return string.IsNullOrEmpty(ProxyString) ? null : new WebProxy(new Uri(ProxyString));

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

/// <summary>
/// 动态根据系统设置更改，因为.net core不能自动监听环境配置
/// </summary>
public class DynamicProxy : IWebProxy
{
    private static readonly MethodInfo? TryCreateMethod;

    static DynamicProxy()
    {
        // 查找 HttpWindowsProxy 类型
        var systemProxyInfoType = Type.GetType("System.Net.Http.SystemProxyInfo, System.Net.Http");
        if (systemProxyInfoType != null)
        {
            TryCreateMethod = systemProxyInfoType.GetMethod("ConstructSystemProxy",
                BindingFlags.Static | BindingFlags.Public);
        }
    }

    static IWebProxy? CurrentProxy()
    {
        if (TryCreateMethod?.Invoke(null, null) is IWebProxy currentProxy)
        {
            return currentProxy;
        }

        return null;
    }

    public Uri? GetProxy(Uri destination)
    {
        try
        {
            var currentProxy = CurrentProxy();
            if (currentProxy != null)
            {
                return currentProxy.GetProxy(destination);
            }
        }
        catch (Exception e)
        {
            Trace.Write($"Get proxy failed:{e.Message}");
        }

        return destination;
    }

    public bool IsBypassed(Uri host)
    {
        var currentProxy = CurrentProxy();
        if (currentProxy != null)
        {
            return currentProxy.IsBypassed(host);
        }

        return false;
    }

    private ICredentials? _credentials;

    public ICredentials? Credentials
    {
        get { return _credentials == null ? _credentials : (CurrentProxy()?.Credentials); }
        set { _credentials = value; }
    }
}