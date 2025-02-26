﻿using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Media;

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

    [JsonIgnore] public ILLMEndpoint? Endpoint { get; set; }

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
                _darkModeIconBrush = Extension.LoadSvgFromBase64(DarkModeIconString);
            }

            return _darkModeIconBrush;
        }
    }

    [JsonPropertyName("light_mode_icon")] public string? LightModeIconString { get; set; }

    [JsonIgnore] public ImageSource? LightModeIcon { get; set; }

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(LightModeIconString))
        {
            LightModeIcon = Extension.LoadSvgFromBase64(LightModeIconString);
        }
        else if (LogoUrl != null)
        {
            LightModeIcon = await Extension.LoadSvgFromHttp(LogoUrl);
        }
    }

    [JsonPropertyName("training_data_date")]
    public string? TrainingDataDate { get; set; }

    [JsonPropertyName("summary")] public string? Summary { get; set; }

    [JsonPropertyName("model_version")] public string? ModelVersion { get; set; }
}