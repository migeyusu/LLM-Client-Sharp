using LLMClient.Configuration;
using LLMClient.UI.ViewModel.Base;

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
}