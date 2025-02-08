using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;
using Svg;

namespace LLMClient.Azure.Models;

public class AzureModelInfo
{
    [JsonPropertyName("friendly_name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string? Id { get; set; }

    [JsonPropertyName("max_input_tokens")] public ulong? MaxInputTokens { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public ulong? MaxOutputTokens { get; set; }

    [JsonPropertyName("original_name")] public string OriginalName { get; set; } = string.Empty;

    [JsonPropertyName("model_family")] public string? ModelFamily { get; set; }

    [JsonPropertyName("description")] public string? DescriptionRaw { get; set; }

    private FlowDocument? _description;

    [JsonIgnore]
    public FlowDocument? Description
    {
        get
        {
            if (DescriptionRaw == null)
            {
                return null;
            }

            if (_description == null)
            {
                _description = DescriptionRaw.ToFlowDocument();
            }

            return _description;
        }
    }

    [JsonPropertyName("publisher")] public string? Publisher { get; set; }

    [JsonPropertyName("logo_url")] public string? LogoUrl { get; set; }

    [JsonPropertyName("notes")] public string? NotesRaw { get; set; }

    private FlowDocument? _notes;

    [JsonIgnore]
    public FlowDocument? Notes
    {
        get
        {
            if (NotesRaw == null)
            {
                return null;
            }

            if (_notes == null)
            {
                _notes = NotesRaw.ToFlowDocument();
            }

            return _notes;
        }
    }

    [JsonPropertyName("evaluation")] public string? EvaluationRaw { get; set; }

    private FlowDocument? _evaluation;

    [JsonIgnore]
    public FlowDocument? Evaluation
    {
        get
        {
            if (EvaluationRaw == null)
            {
                return null;
            }

            if (_evaluation == null)
            {
                _evaluation = EvaluationRaw.ToFlowDocument();
            }

            return _evaluation;
        }
    }

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
            if (_lightModeIconBrush != null)
            {
                return _lightModeIconBrush;
            }

            if (!string.IsNullOrEmpty(LightModeIconString))
            {
                _lightModeIconBrush = Extension.LoadImage(LightModeIconString);
            }
            
            /*if (LogoUrl != null)
            {
                if (_lightModeIconBrush == null)
                {
                    var message = new HttpClient().GetAsync($"https://github.com{LogoUrl}").GetAwaiter().GetResult();
                    if (message.StatusCode==HttpStatusCode.OK)
                    {
                        message.Content
                        _lightModeIconBrush =
                    }

                }
            }*/

            return _lightModeIconBrush;
        }
    }

    [JsonPropertyName("training_data_date")]
    public string? TrainingDataDate { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("model_version")] public string? ModelVersion { get; set; }
}