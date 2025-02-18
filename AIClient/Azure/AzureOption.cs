using LLMClient.UI;

namespace LLMClient.Azure;

public class AzureOption : BaseViewModel
{
    private string _apiToken = "xxxxx";

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

    private string _url = "https://models.inference.ai.azure.com";

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