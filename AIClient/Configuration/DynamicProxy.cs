using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace LLMClient.Configuration;

/// <summary>
/// 动态根据系统设置更改，因为.net core不能自动监听环境配置
/// </summary>
public class DynamicProxy : IWebProxy
{
    public static DynamicProxy Instance { get; } = new DynamicProxy();

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