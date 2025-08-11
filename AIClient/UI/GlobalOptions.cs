using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Dialog;
using LLMClient.Rag;
using LLMClient.Rag.Document;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class GlobalOptions : NotifyDataErrorInfoViewModelBase
{
    public GlobalOptions()
    {
        PopupSelectViewModel = PopupSelectViewModel =
            new ModelSelectionPopupViewModel((model =>
                {
                    var llmClient = model.GetClient();
                    if (llmClient == null)
                    {
                        return;
                    }

                    this.SummarizeClient = llmClient;
                }))
                { SuccessRoutedCommand = PopupBox.ClosePopupCommand };
    }

    private const string DEFAULT_GLOBAL_CONFIG_FILE = "globalconfig.json";

    private const string DefaultSummarizePrompt =
        "Provide a concise and complete summarization of the entire dialog that does not exceed {0} words. \n\nThis summary must always:\n- Consider both user and assistant interactions\n- Maintain continuity for the purpose of further dialog\n- Include details from any existing summary\n- Focus on the most significant aspects of the dialog\n\nThis summary must never:\n- Critique, correct, interpret, presume, or assume\n- Identify faults, mistakes, misunderstanding, or correctness\n- Analyze what has not occurred\n- Exclude details from any existing summary";

    [JsonPropertyName("TokenSummarizePrompt")]
    public string TokenSummarizePromptString { get; set; } = DefaultSummarizePrompt;

    [JsonIgnore]
    public string TokenSummarizePrompt
    {
        get { return string.Format(TokenSummarizePromptString, SummarizeWordsCount); }
    }

    private int _summarizeWordsCount = 1000;

    public int SummarizeWordsCount
    {
        get => _summarizeWordsCount;
        set
        {
            this.ClearError();
            if (value == _summarizeWordsCount) return;
            if (value < 100)
            {
                this.AddError("Summarize words count must be greater than 100.");
                return;
            }

            _summarizeWordsCount = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore]
    public ILLMChatClient? SummarizeClient
    {
        get
        {
            if (SummarizeModelPersistModel == null)
            {
                return null;
            }

            return ServiceLocator.GetService<IMapper>()?
                .Map<LLMClientPersistModel, ILLMChatClient>(SummarizeModelPersistModel);
        }
        set
        {
            if (value == null)
            {
                SummarizeModelPersistModel = null;
                return;
            }

            SummarizeModelPersistModel = ServiceLocator.GetService<IMapper>()?
                .Map<ILLMChatClient, LLMClientPersistModel>(value, (options => { }));
        }
    }

    private LLMClientPersistModel? _summarizeModelPersistModel;

    public LLMClientPersistModel? SummarizeModelPersistModel
    {
        get => _summarizeModelPersistModel;
        set
        {
            if (Equals(value, _summarizeModelPersistModel)) return;
            _summarizeModelPersistModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SummarizeClient));
        }
    }

    [JsonIgnore] public ModelSelectionPopupViewModel PopupSelectViewModel { get; }

    public GoogleSearchOption GoogleSearchOption { get; set; } = new GoogleSearchOption();

    public RagOption RagOption { get; set; } = new RagOption();

    [JsonIgnore]
    public ICommand SaveCommand => new ActionCommand(async (param) =>
    {
        var fileInfo = new FileInfo(DEFAULT_GLOBAL_CONFIG_FILE);
        fileInfo.Directory?.Create();
        using (var fileStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(fileStream, this);
        }
    });

    public static async Task<GlobalOptions> LoadOrCreate()
    {
        var fileInfo = new FileInfo(DEFAULT_GLOBAL_CONFIG_FILE);
        if (fileInfo.Exists)
        {
            try
            {
                using (var fileStream = fileInfo.OpenRead())
                {
                    var config = await JsonSerializer.DeserializeAsync<GlobalOptions>(fileStream);
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