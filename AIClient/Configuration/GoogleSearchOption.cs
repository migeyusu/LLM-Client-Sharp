using System.Diagnostics.CodeAnalysis;
using LLMClient.UI.ViewModel.Base;

namespace LLMClient.Configuration;

public class GoogleSearchOption : BaseViewModel<GoogleSearchOption>, ICloneable
{
    private string? _apiKey;
    private string? _searchEngineId;

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

    public ProxySetting ProxySetting { get; set; } = new ProxySetting();

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
        return HashCode.Combine(_apiKey, _searchEngineId, ProxySetting);
    }

    public object Clone()
    {
        return new GoogleSearchOption()
        {
            ApiKey = this._apiKey,
            SearchEngineId = this._searchEngineId,
            ProxySetting = this.ProxySetting
        };
    }
}