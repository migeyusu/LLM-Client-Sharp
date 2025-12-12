using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Input;
using System.Windows.Media;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Endpoints.Converters;
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
            _iconUrl = value;
            OnPropertyChanged();
            if (!string.IsNullOrEmpty(value) && Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
            {
                this.Icon.LightModeUri = uri;
            }
            else
            {
                this.Icon.LightModeUri = null;
            }
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

    public APIDefaultOption ConfigOption { get; set; } = new();

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
            foreach (var apiModelInfo in Models)
            {
                apiModelInfo.IsNotMatchFromSource = !modelMapping.MapInfo(apiModelInfo);
            }
        }

        MessageEventBus.Publish("已刷新模型列表");
    }));

    private ObservableCollection<APIModelInfo> _models = [];

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

    [JsonIgnore] public virtual UriThemedIcon Icon { get; } = new(null, ImageExtensions.APIIconImage);

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
}