using LLMClient.UI.ViewModel.Base;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Configuration;

public class ProxySetting : BaseViewModel<ProxySetting>
{
    private bool _useGlobalProxy = true;

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

    public ProxyOption GetRealProxy()
    {
        return UseGlobalProxy
            ? ServiceLocator.GetService<GlobalOptions>()!.ProxyOption
            : this.ProxyOption;
    }

    protected bool Equals(ProxySetting other)
    {
        return _useGlobalProxy == other._useGlobalProxy && ProxyOption.Equals(other.ProxyOption);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ProxySetting)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_useGlobalProxy, ProxyOption);
    }
}