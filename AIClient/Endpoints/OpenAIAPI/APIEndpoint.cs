using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIEndPoint : NotifyDataErrorInfoViewModelBase, ILLMEndpoint
{
    public const string KeyName = "OpenAIAPICompatible";

    public ObservableCollection<APIModelInfo> Models
    {
        get => _models;
        set
        {
            if (Equals(value, _models)) return;
            _models = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AvailableModelNames));
            OnPropertyChanged(nameof(AddNewCommand));
            OnPropertyChanged(nameof(RemoveCommand));
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (value == _displayName) return;
            ClearError();
            if (value.Length > 26)
            {
                AddError("The display name must be less than 26 characters.");
                return;
            }

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public string Name { get; set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public ImageSource? Icon
    {
        get { return _icon ?? APIClient.IconImageSource; }
        private set
        {
            if (Equals(value, _icon)) return;
            _icon = value;
            OnPropertyChangedAsync();
        }
    }

    public string? IconUrl
    {
        get => _iconUrl;
        set
        {
            if (value == _iconUrl) return;
            ClearError();
            if ((!string.IsNullOrEmpty(value)) &&
                !Extension.SupportedImageExtensions.Contains(Path.GetExtension(value)))
            {
                AddError("The image extension is not supported.");
                return;
            }

            _iconUrl = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    public DefaultOption ConfigOption
    {
        get => _configOption;
        set
        {
            if (Equals(value, _configOption)) return;
            _configOption = value;
            OnPropertyChanged();
        }
    }

    private string _displayName = string.Empty;
    private DefaultOption _configOption = new DefaultOption();
    private ObservableCollection<APIModelInfo> _models = new ObservableCollection<APIModelInfo>();
    private string? _iconUrl;
    private ImageSource? _icon = null;

    [JsonIgnore] public IList<string> AvailableModelNames => Models.Select(x => x.Name).ToList();

    private const string NewModelName = "测试名称";

    [JsonIgnore]
    public ICommand AddNewCommand =>
        new ActionCommand((o =>
        {
            int v = 0;
            var newModelName = NewModelName + v;
            while (_models.Any((info => info.Name == newModelName)))
            {
                v++;
                newModelName = NewModelName + v;
            }

            Models.Add(new APIModelInfo() { Name = newModelName, Endpoint = this });
        }));

    [JsonIgnore] public ICommand RemoveCommand => new ActionCommand((o => { Models.Remove((APIModelInfo)o); }));

    public ILLMModelClient? NewClient(string modelName)
    {
        var firstOrDefault = Models.FirstOrDefault(x => x.Name == modelName);
        if (firstOrDefault == null)
            return null;
        return new APIClient(this, firstOrDefault, ConfigOption);
    }

    public Task InitializeAsync()
    {
        foreach (var model in _models)
        {
            model.Endpoint = this;
        }

        UpdateIcon();
        return Task.CompletedTask;
        /*var path = Path.GetFullPath(Path.Combine("EndPoints", "Compatible", "Models", "models.json"));

        new FileInfo(path);*/
    }

    private async void UpdateIcon()
    {
        if (!string.IsNullOrEmpty(IconUrl))
        {
            this.Icon = await this.IconUrl.LoadImageAsync();
        }
    }
}