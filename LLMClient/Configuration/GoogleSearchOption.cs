using System.Diagnostics.CodeAnalysis;
using Google.Apis.Services;
using LLMClient.Component.ViewModel.Base;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;

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

    public ProxySetting ProxySetting { get; set; } = new();

    [MemberNotNullWhen(true, nameof(ApiKey), nameof(SearchEngineId))]
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(SearchEngineId);
    }
    
    
    private GoogleSearchOption? _optionSnap;

#pragma warning disable SKEXP0050
    private GoogleTextSearch? _textSearch;
#pragma warning restore SKEXP0050

    public ITextSearch? GetTextSearch()
    {
        if (this.IsValid() && !this.PublicEquals(_optionSnap))
        {
            var proxyOption = this.ProxySetting.GetRealProxy();
#pragma warning disable SKEXP0050
            _textSearch = new GoogleTextSearch(initializer: new BaseClientService.Initializer()
#pragma warning restore SKEXP0050
            {
                ApiKey = this.ApiKey,
                HttpClientFactory = proxyOption.CreateFactory(),
            }, this.SearchEngineId);
            _optionSnap = (GoogleSearchOption)this.Clone();
        }

        return _textSearch;
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