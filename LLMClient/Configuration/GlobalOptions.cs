using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Rag;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Data;
using Microsoft.Xaml.Behaviors.Core;
using MessageBox = System.Windows.MessageBox;

namespace LLMClient.Configuration;

//do not separate persistence and view model, because global option is simple enough.
public class GlobalOptions : NotifyDataErrorInfoViewModelBase
{
    public GlobalOptions()
    {
        ContextSummaryPopupSelectViewModel = new ModelSelectionPopupViewModel(this.ApplyContextSummarizeClient)
            { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        SubjectSummaryPopupViewModel =
            new ModelSelectionPopupViewModel(this.ApplySubjectSummarizeClient)
                { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
        TextFormatterPopupViewModel =
            new ModelSelectionPopupViewModel(this.ApplyTextFormatterClient)
                { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
    }

    public static string DefaultConfigFile
    {
        get
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(DEFAULT_GLOBAL_CONFIG_FILE, baseDirectory);
        }
    }

    public const string DEFAULT_GLOBAL_CONFIG_FILE = "globalconfig.json";

    private static IMapper Mapper => _mapperLazy.Value;

    private static Lazy<IMapper> _mapperLazy = new(() => ServiceLocator.GetService<IMapper>()!);

    #region context summarize

    private const string DefaultContextSummarizePrompt =
        "Provide a concise and complete summarization of the entire dialog that does not exceed {0} words. \n\nThis summary must always:\n- Consider both user and assistant interactions\n- Maintain continuity for the purpose of further dialog\n- Include details from any existing summary\n- Focus on the most significant aspects of the dialog\n\nThis summary must never:\n- Critique, correct, interpret, presume, or assume\n- Identify faults, mistakes, misunderstanding, or correctness\n- Analyze what has not occurred\n- Exclude details from any existing summary";

    [JsonIgnore] public ModelSelectionPopupViewModel ContextSummaryPopupSelectViewModel { get; }

    [JsonPropertyName("TokenSummarizePrompt")]
    public string ContextSummarizePromptString { get; set; } = DefaultContextSummarizePrompt;

    [JsonIgnore]
    public string ContextSummarizePrompt
    {
        get { return string.Format(ContextSummarizePromptString, ContextSummarizeWordsCount); }
    }

    private int _contextSummarizeWordsCount = 1000;

    [JsonPropertyName("SummarizeWordsCount")]
    public int ContextSummarizeWordsCount
    {
        get => _contextSummarizeWordsCount;
        set
        {
            this.ClearError();
            if (value == _contextSummarizeWordsCount) return;
            if (value < 100)
            {
                this.AddError("Summarize words count must be greater than 100.");
            }

            _contextSummarizeWordsCount = value;
            OnPropertyChanged();
        }
    }

    public ILLMChatClient? CreateContextSummarizeClient()
    {
        if (ContextSummarizeClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(ContextSummarizeClientPersist, (options => { }));
    }

    public void ApplyContextSummarizeClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            ContextSummarizeClientPersist = null;
            return;
        }

        ContextSummarizeClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    private ParameterizedLLMModelPO? _summarizeModelPersistModel;

    [JsonPropertyName("SummarizeModelPersistModel")]
    public ParameterizedLLMModelPO? ContextSummarizeClientPersist
    {
        get => _summarizeModelPersistModel;
        set
        {
            if (Equals(value, _summarizeModelPersistModel)) return;
            _summarizeModelPersistModel = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region subject summary

    public bool EnableAutoSubjectGeneration { get; set; } = true;

    [JsonIgnore] public ModelSelectionPopupViewModel SubjectSummaryPopupViewModel { get; }

    private const string DefaultSubjectSummarizePrompt =
        "Give a title of the dialog that does not exceed {0} words.";

    [JsonPropertyName("SubjectSummarizePrompt")]
    public string SubjectPromptString { get; set; } = DefaultSubjectSummarizePrompt;

    [JsonIgnore]
    public string SubjectSummarizePrompt
    {
        get { return string.Format(SubjectPromptString, 10); }
    }

    public ILLMChatClient? CreateSubjectSummarizeClient()
    {
        if (!EnableAutoSubjectGeneration)
        {
            return null;
        }

        if (SubjectSummarizeClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(SubjectSummarizeClientPersist, (options => { }));
    }

    public void ApplySubjectSummarizeClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            SubjectSummarizeClientPersist = null;
            return;
        }

        SubjectSummarizeClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    private ParameterizedLLMModelPO? _subjectSummarizeClientPersist;

    [JsonPropertyName("SubjectSummarizeClient")]
    public ParameterizedLLMModelPO? SubjectSummarizeClientPersist
    {
        get => _subjectSummarizeClientPersist;
        set
        {
            if (Equals(value, _subjectSummarizeClientPersist)) return;
            _subjectSummarizeClientPersist = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region text formatter

    [JsonIgnore] public ModelSelectionPopupViewModel TextFormatterPopupViewModel { get; }

    public ILLMChatClient? CreateTextFormatterClient()
    {
        if (TextFormatterClientPersist == null)
        {
            return null;
        }

        return Mapper?
            .Map<ParameterizedLLMModelPO, ILLMChatClient>(TextFormatterClientPersist, (options => { }));
    }

    public void ApplyTextFormatterClient(IParameterizedLLMModel? value)
    {
        if (value == null)
        {
            TextFormatterClientPersist = null;
            return;
        }

        TextFormatterClientPersist = Mapper?
            .Map<IParameterizedLLMModel, ParameterizedLLMModelPO>(value, (options => { }));
    }

    private ParameterizedLLMModelPO? _textFormatterClientPersist;

    [JsonPropertyName("TextFormatterClientPersist")]
    public ParameterizedLLMModelPO? TextFormatterClientPersist
    {
        get => _textFormatterClientPersist;
        set
        {
            if (Equals(value, _textFormatterClientPersist)) return;
            _textFormatterClientPersist = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region search

    public GoogleSearchOption GoogleSearchOption { get; set; } = new();

    public ITextSearch? GetTextSearch()
    {
        return GoogleSearchOption.GetTextSearch();
    }

    /// <summary>
    /// as global option
    /// </summary>
    public ProxyOption ProxyOption { get; set; } = new ProxyOption();

    #endregion

    public RagOption RagOption { get; set; } = new RagOption();

    /*public ObservableCollection<ILLMChatModel> SuggestedModels { get; } =
        new ObservableCollection<ILLMChatModel>();*/

    [JsonIgnore]
    public ICommand SaveCommand => new ActionCommand(async (param) =>
    {
        if (HasErrors)
        {
            MessageEventBus.Publish("Cannot save global configuration due to validation errors.");
            return;
        }

        var currentDirectory = Directory.GetCurrentDirectory();
        var configFilePath = Path.GetFullPath(DefaultConfigFile, currentDirectory);
        try
        {
            await this.SaveJsonToFileAsync(configFilePath, Extension.DefaultJsonSerializerOptions);
        }
        catch (Exception e)
        {
            MessageBox.Show("Failed to save global configuration: " + e.Message, "Error");
        }

        MessageEventBus.Publish("Global configuration saved successfully.");
    });

    public static async Task<GlobalOptions> LoadOrCreate(string? configFilePath = DEFAULT_GLOBAL_CONFIG_FILE)
    {
        configFilePath ??= DefaultConfigFile;
        var currentDirectory = Directory.GetCurrentDirectory();
        configFilePath = Path.GetFullPath(configFilePath, currentDirectory);
        var fileInfo = new FileInfo(configFilePath);
        if (fileInfo.Exists)
        {
            try
            {
                using (var fileStream = fileInfo.OpenRead())
                {
                    var config =
                        await JsonSerializer.DeserializeAsync<GlobalOptions>(fileStream,
                            Extension.DefaultJsonSerializerOptions);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        return new GlobalOptions();
    }
}