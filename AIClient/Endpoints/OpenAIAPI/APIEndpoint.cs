using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints.Converters;
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

    public virtual bool IsDefault { get; } = false;

    public string Name { get; set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public virtual ImageSource Icon
    {
        get { return _icon ?? ImageExtensions.EndpointIcon; }
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
                !ImageExtensions.SupportedImageExtensions.Contains(Path.GetExtension(value)))
            {
                AddError("The image extension is not supported.");
                return;
            }

            _iconUrl = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    public ModelSource ModelsSource
    {
        get => _modelsSource;
        set
        {
            if (value == _modelsSource) return;
            _modelsSource = value;
            OnPropertyChanged();
            ModelMapping = ModelMapping.Create(value);
        }
    }

    [JsonIgnore] public ModelMapping? ModelMapping { get; set; }

    public ICommand RefreshModelSource => new ActionCommand((async o =>
    {
        if (ModelMapping == null)
        {
            return;
        }

        if (await ModelMapping.Refresh())
        {
            foreach (var apiModelInfo in this.Models)
            {
                if (!ModelMapping.MapInfo(apiModelInfo))
                {
                    apiModelInfo.IsNotAvailable = true;
                }
            }
        }

        MessageEventBus.Publish("已刷新模型列表");
    }));

    public string? ApiLogUrl
    {
        get => _apiLogUrl;
        set
        {
            if (value == _apiLogUrl) return;
            _apiLogUrl = value;
            OnPropertyChanged();
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
    private int _selectedModelIndex = -1;
    private string? _apiLogUrl;
    private ModelSource _modelsSource = ModelSource.None;

    [JsonIgnore]
    public int SelectedModelIndex
    {
        get => _selectedModelIndex;
        set
        {
            if (value == _selectedModelIndex) return;
            _selectedModelIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AddNewCommand));
        }
    }

    [JsonIgnore] public IReadOnlyCollection<string> AvailableModelNames => Models.Select(x => x.Name).ToArray();

    private const string NewModelName = "测试名称";

    [JsonIgnore]
    public ICommand AddNewCommand => new ActionCommand((o =>
    {
        int v = 0;
        var newModelName = NewModelName + v;
        while (_models.Any((info => info.Name == newModelName)))
        {
            v++;
            newModelName = NewModelName + v;
        }

        var apiModelInfo = new APIModelInfo() { Name = newModelName, Endpoint = this };
        if (SelectedModelIndex > -1)
        {
            Models.Insert(SelectedModelIndex + 1, apiModelInfo);
        }
        else
        {
            Models.Add(apiModelInfo);
        }

        SelectedModelIndex = Models.IndexOf(apiModelInfo);
        OnPropertyChanged(nameof(AvailableModelNames));
    }));

    [JsonIgnore]
    public ICommand RemoveCommand => new ActionCommand((o =>
    {
        Models.Remove((APIModelInfo)o);
        OnPropertyChanged(nameof(AvailableModelNames));
    }));

    public ILLMModelClient? NewClient(string modelName)
    {
        var apiModelInfo = Models.FirstOrDefault(x => x.Name == modelName);
        if (apiModelInfo == null)
            return null;
        return new APIClient(this, apiModelInfo, ConfigOption);
    }

    public ILLMModel? GetModel(string modelName)
    {
        return Models.FirstOrDefault(x => x.Name == modelName);
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

    /// <summary>
    /// 验证
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(DisplayName))
        {
            errorMessage = "Display name cannot be empty.";
            return false;
        }

        if (string.IsNullOrEmpty(IconUrl))
        {
            errorMessage = "Icon URL cannot be empty.";
            return false;
        }

        var distinctBy = _models.DistinctBy((info => info.Name));
        var apiModelInfos = _models.Except(distinctBy);
        if (apiModelInfos.Any())
        {
            errorMessage = "Model name must be unique.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private async void UpdateIcon()
    {
        if (!string.IsNullOrEmpty(IconUrl))
        {
            this._icon = await new Uri(this.IconUrl).GetIcon();
            OnPropertyChangedAsync(nameof(Icon));
        }
    }

    public void MoveUp(APIModelInfo modelInfo)
    {
        int index = Models.IndexOf(modelInfo);
        if (index > 0)
        {
            Models.Move(index, index - 1);
            SelectedModelIndex = index - 1;
        }
    }
}