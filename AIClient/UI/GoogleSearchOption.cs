using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata.Ecma335;
using LLMClient.UI.Component;

namespace LLMClient.UI;

public class GoogleSearchOption : BaseViewModel, ICloneable
{
    private string? _apiKey;
    private string? _searchEngineId;
    private bool _useGlobalProxy = true;

    public string? ApiKey
    {
        get => _apiKey;
        set
        {
            if (value == _apiKey) return;
            _apiKey = value;
            OnPropertyChanged();
        }
    }

    public string? SearchEngineId
    {
        get => _searchEngineId;
        set
        {
            if (value == _searchEngineId) return;
            _searchEngineId = value;
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

    public ProxyOption ProxyOption { get; set; } = new();

    [MemberNotNullWhen(true, nameof(ApiKey), nameof(SearchEngineId))]
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(SearchEngineId);
    }

    protected bool Equals(GoogleSearchOption other)
    {
        return _apiKey == other._apiKey && _searchEngineId == other._searchEngineId;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((GoogleSearchOption)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_apiKey, _searchEngineId);
    }

    public object Clone()
    {
        return new GoogleSearchOption()
        {
            _apiKey = this._apiKey,
            _searchEngineId = this._searchEngineId
        };
    }
}