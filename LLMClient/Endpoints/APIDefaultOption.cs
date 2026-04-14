using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;

namespace LLMClient.Endpoints;

public class APIDefaultOption : BaseViewModel<APIDefaultOption>
{
    private string _apiToken = string.Empty;

    public string APIToken
    {
        get => _apiToken;
        set
        {
            if (value == _apiToken) return;
            _apiToken = value;
            OnPropertyChanged();
        }
    }

    private string _url = string.Empty;
    
    private bool _isOpenAiCompatible = true;

    private bool _treatNullChoicesAsEmptyResponse;

    private string? _userAgentPrefix;

    public string? UserAgentPrefix
    {
        get => _userAgentPrefix;
        set
        {
            if (value == _userAgentPrefix) return;
            _userAgentPrefix = value;
            OnPropertyChanged();
        }
    }

    private string? _xRequestedWith;

    public string? XRequestedWith
    {
        get => _xRequestedWith;
        set
        {
            if (value == _xRequestedWith) return;
            _xRequestedWith = value;
            OnPropertyChanged();
        }
    }

    public string URL
    {
        get => _url;
        set
        {
            if (value == _url) return;
            _url = value;
            OnPropertyChanged();
        }
    }

    public ProxySetting ProxySetting { get; set; } = new ProxySetting();

    public bool IsOpenAICompatible
    {
        get => _isOpenAiCompatible;
        set
        {
            if (value == _isOpenAiCompatible) return;
            _isOpenAiCompatible = value;
            OnPropertyChanged();
        }
    }

    public bool TreatNullChoicesAsEmptyResponse
    {
        get => _treatNullChoicesAsEmptyResponse;
        set
        {
            if (value == _treatNullChoicesAsEmptyResponse) return;
            _treatNullChoicesAsEmptyResponse = value;
            OnPropertyChanged();
        }
    }
}