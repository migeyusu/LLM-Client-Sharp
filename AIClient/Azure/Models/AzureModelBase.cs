using System.Text.Json.Serialization;
using System.Windows.Media;

namespace LLMClient.Azure.Models;

public class AzureModelBase : BaseViewModel, ILLMModel
{
    private AzureClient? _client;

    public AzureModelInfo ModelInfo { get; }

    public string Name
    {
        get { return ModelInfo.Name; }
    }

    public string? Id
    {
        get { return ModelInfo.Id; }
    }

    public ImageSource? Icon
    {
        get { return UITheme.IsDarkMode ? ModelInfo.DarkModeIcon : ModelInfo.LightModeIcon; }
    }

    public AzureModelBase(AzureClient? client, AzureModelInfo modelInfo)
    {
        _client = client;
        ModelInfo = modelInfo;
        UITheme.ModeChanged += UIThemeOnModeChanged;
    }

    ~AzureModelBase()
    {
        UITheme.ModeChanged -= UIThemeOnModeChanged;
    }

    private void UIThemeOnModeChanged(bool obj)
    {
        this.OnPropertyChanged(nameof(Icon));
    }

    public ILLMClient GetClient()
    {
        return _client ?? throw new NullReferenceException();
    }
}

public class AzureModelInfo
{
    [JsonPropertyName("friendly_name")] public string Name { get; set; }

    [JsonPropertyName("name")] public string? Id { get; set; }

    [JsonPropertyName("max_input_tokens")] public ulong? MaxInputTokens { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public ulong? MaxOutputTokens { get; set; }

    [JsonPropertyName("original_name")] public string? OriginalName { get; set; }

    [JsonPropertyName("model_family")] public string? ModelFamily { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("notes")] public string? Notes { get; set; }

    [JsonPropertyName("evaluation")] public string? Evaluation { get; set; }

    [JsonPropertyName("dark_mode_icon")] public string? DarkModeIconString { get; set; }

    private ImageSource? _darkModeIconBrush = null;

    [JsonIgnore]
    public ImageSource? DarkModeIcon
    {
        get
        {
            if (string.IsNullOrEmpty(DarkModeIconString))
                return null;
            if (_darkModeIconBrush == null)
            {
                _darkModeIconBrush = Extension.LoadImage(DarkModeIconString);
            }

            return _darkModeIconBrush;
        }
    }

    [JsonPropertyName("light_mode_icon")] public string? LightModeIconString { get; set; }

    private ImageSource? _lightModeIconBrush = null;

    [JsonIgnore]
    public ImageSource? LightModeIcon
    {
        get
        {
            if (string.IsNullOrEmpty(LightModeIconString))
                return null;
            if (_lightModeIconBrush == null)
            {
                _lightModeIconBrush = Extension.LoadImage(LightModeIconString);
            }

            return _lightModeIconBrush;
        }
    }

    [JsonPropertyName("training_data_date")]
    public string? TrainingDataDate { get; set; }

    [JsonPropertyName("summary")] public string? Summery { get; set; }

    [JsonPropertyName("model_version")] public string? ModelVersion { get; set; }
}