using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Data;
using LLMClient.Endpoints.Converters;
using LLMClient.UI.Component.Utility;
using LLMClient.UI.ViewModel.Base;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Endpoints.OpenAIAPI;

public class APIEndPointOption : NotifyDataErrorInfoViewModelBase
{
    public string Name { get; set; } = Guid.NewGuid().ToString();

    private string _displayName = string.Empty;

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

    private string? _iconUrl;

    public string? IconUrl
    {
        get => _iconUrl;
        set
        {
            if (value == _iconUrl) return;
            ClearError();
            _iconUrl = value;
            OnPropertyChanged();
            UpdateIcon();
        }
    }

    private ModelSource _modelsSource = ModelSource.None;

    public ModelSource ModelsSource
    {
        get => _modelsSource;
        set
        {
            if (value == _modelsSource) return;
            _modelsSource = value;
            OnPropertyChanged();
        }
    }

    private string? _apiLogUrl;

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
    
    public APIDefaultOption ConfigOption { get; set; } = new APIDefaultOption();

    private ModelMapping? ModelMapping => ModelMapping.Create(this.ModelsSource);

    public ICommand RefreshModelSource => new ActionCommand((async o =>
    {
        var modelMapping = ModelMapping;
        if (modelMapping == null)
        {
            return;
        }

        if (await modelMapping.Refresh())
        {
            foreach (var apiModelInfo in this.Models)
            {
                apiModelInfo.IsNotMatchFromSource = !modelMapping.MapInfo(apiModelInfo);
            }
        }

        MessageEventBus.Publish("已刷新模型列表");
    }));

    private ObservableCollection<APIModelInfo> _models = new ObservableCollection<APIModelInfo>();

    public ObservableCollection<APIModelInfo> Models
    {
        get => _models;
        set
        {
            if (Equals(value, _models)) return;
            _models = value;
            OnPropertyChanged();
        }
    }

    private ImageSource? _icon = null;


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

    public async void UpdateIcon()
    {
        if (!string.IsNullOrEmpty(IconUrl) && Uri.TryCreate(this.IconUrl, UriKind.RelativeOrAbsolute, out var uri))
        {
            this._icon = await uri.GetImageSourceAsync();
            OnPropertyChangedAsync(nameof(Icon));
        }
        else
        {
            this._icon = null;
        }
    }
}