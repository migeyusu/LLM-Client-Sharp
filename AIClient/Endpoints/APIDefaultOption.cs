using LLMClient.UI;

namespace LLMClient.Endpoints;

public class APIDefaultOption : BaseViewModel
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
}