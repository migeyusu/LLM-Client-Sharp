using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LLMClient.Dialog;

public class RequesterViewModel : BaseViewModel
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = true;

    public event Action<IResponse>? RequestCompleted;

    public ICommand NewRequestCommand => new ActionCommand(async o =>
    {
        try
        {
            var requestViewItem = this.NewRequest();
            if (requestViewItem == null)
            {
                return;
            }

            IsNewResponding = true;
            using (_tokenSource = new CancellationTokenSource())
            {
                var completedResult =
                    await _getResponse.Invoke(this.DefaultClient, requestViewItem, null, _tokenSource.Token);
                OnRequestCompleted(completedResult);
                if (!completedResult.IsInterrupt)
                {
                    ClearRequest();
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
        finally
        {
            IsNewResponding = false;
        }
    });

    public ICommand SummarizeCommand => new ActionCommand(_ => { Summarize(); });

    public ICommand ChangeModelCommand => new ActionCommand(async o =>
    {
        var selectionViewModel = new ModelSelectionPopupViewModel((model =>
        {
            this.DefaultClient = model.CreateClient();
        }));
        await DialogHost.Show(selectionViewModel);
    });

    private ILLMChatClient _defaultClient;

    public ILLMChatClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChanged;
            }

            _defaultClient = value;
            BindClient(value);
            OnPropertyChanged();
        }
    }

    #region rag

    private readonly IRagSourceCollection _ragSourceCollection;

    //rag有两种利用模式：search和plugin模式，search模式由手动调用，可产生结果并入上下文；plugin由llm驱动调用

    private IList<SelectableViewModel<IRagSource>> _ragSources = Array.Empty<SelectableViewModel<IRagSource>>();

    //不需要持久化
    public IList<SelectableViewModel<IRagSource>> RagSources
    {
        get => _ragSources;
        set
        {
            if (Equals(value, _ragSources)) return;
            _ragSources = value;
            OnPropertyChanged();
        }
    }

    public void NotifyRagSelection()
    {
        OnPropertyChanged(nameof(IsRagEnabled));
    }

    public bool IsRagEnabled => RagSources.Any(model => model.IsSelected && model.Data.IsAvailable);

    public void RefreshRagSources()
    {
        var selected = RagSources.Where(model => model.IsSelected).ToList();
        var selectableViewModels = _ragSourceCollection.Sources.ToSelectable().ToArray();
        foreach (var selectableViewModel in selectableViewModels)
        {
            var dataId = selectableViewModel.Data.Id;
            if (selected.Find((model => model.Data.Id == dataId)) != null)
            {
                selectableViewModel.IsSelected = true;
            }
        }

        RagSources = selectableViewModels;
    }

    public FileQueryViewModel QueryViewModel { get; }

    #endregion

    #region attachment

    /// <summary>
    /// 这里的Attachment不会持久化，只有发出之后的请求中附带的attchment会被持久化（缓存）。
    /// </summary>
    public ObservableCollection<Attachment> Attachments { get; set; } =
        new();

    public ICommand AddImageCommand => new ActionCommand(o =>
    {
        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var fileName in openFileDialog.FileNames)
        {
            this.Attachments.Add(Attachment.CreateFromLocal(fileName, AttachmentType.Image));
        }
    });

    public ICommand RemoveAttachmentCommand => new ActionCommand(o =>
    {
        if (o is Attachment attachment)
        {
            this.Attachments.Remove(attachment);
        }
    });

    #endregion

    #region function call

    public AIFunctionTreeSelectorViewModel FunctionTreeSelector { get; }

    #endregion

    public SearchConfigViewModel SearchConfig { get; }

    #region input

    private string? _promptString;

    public string? PromptString
    {
        get => _promptString;
        set
        {
            if (value == _promptString) return;
            _promptString = value;
            OnPropertyChanged();
            InvalidateAsyncProperty(nameof(EstimatedTokens));
        }
    }

    public long EstimatedTokens
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                if (string.IsNullOrEmpty(_promptString))
                {
                    return 0;
                }

                return await _tokensCounter.CountTokens(_promptString);
            });
        }
    }

    public ICommand CancelLastCommand => new ActionCommand(_ => { _tokenSource?.Cancel(); });

    #endregion

    private readonly Func<ILLMChatClient, IRequestItem, int?, CancellationToken, Task<CompletedResult>> _getResponse;
    private readonly GlobalOptions _options;

    private readonly ITokensCounter _tokensCounter;

    public RequesterViewModel(ILLMChatClient modelClient,
        Func<ILLMChatClient, IRequestItem, int?, CancellationToken, Task<CompletedResult>> getResponse,
        GlobalOptions options, IRagSourceCollection ragSourceCollection, ITokensCounter tokensCounter)
    {
        FunctionTreeSelector = new AIFunctionTreeSelectorViewModel();
        SearchConfig = new SearchConfigViewModel();
        QueryViewModel = new FileQueryViewModel(this);
        this._defaultClient = modelClient;
        this._getResponse = getResponse;
        _options = options;
        _ragSourceCollection = ragSourceCollection;
        _tokensCounter = tokensCounter;
        this.BindClient(modelClient);
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(EstimatedTokens))
            {
                return;
            }

            this.IsDataChanged = true;
        };
    }

    private void BindClient(ILLMChatClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChanged;
        }

        this.SearchConfig.ResetSearch(client);
        this.FunctionTreeSelector.SelectableCallEngineTypes = DefaultClient.Model.SupportFunctionCall
            ?
            [
                FunctionCallEngineType.OpenAI,
                FunctionCallEngineType.Prompt
            ]
            :
            [
                FunctionCallEngineType.Prompt
            ];
    }

    public void ClearRequest()
    {
        this.Attachments.Clear();
        this.PromptString = null;
    }

    private bool _isNewResponding;

    public bool IsNewResponding
    {
        get => _isNewResponding;
        private set
        {
            if (value == _isNewResponding) return;
            _isNewResponding = value;
            OnPropertyChanged();
        }
    }

    public IFunctionGroupSource? FunctionGroupSource { get; set; }

    private CancellationTokenSource? _tokenSource;

    public async void Summarize(int? index = null)
    {
        IsNewResponding = true;
        try
        {
            var summaryRequest = new SummaryRequestViewItem()
            {
                SummaryPrompt = _options.ContextSummarizePrompt,
                OutputLength = _options.ContextSummarizeWordsCount,
                InteractionId = Guid.NewGuid(),
            };

            var summarizeModel = _options.CreateContextSummarizeClient() ?? this.DefaultClient;
            using (_tokenSource = new CancellationTokenSource())
            {
                await _getResponse.Invoke(summarizeModel, summaryRequest, index, _tokenSource.Token);
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
        finally
        {
            IsNewResponding = false;
        }
    }

    public RequestViewItem? NewRequest(string additionalPrompt = "")
    {
        if (string.IsNullOrEmpty(this.PromptString))
        {
            return null;
        }

        var promptBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(additionalPrompt))
        {
            promptBuilder.Append(additionalPrompt);
        }

        promptBuilder.Append(PromptString);
        IList<CheckableFunctionGroupTree>? tools = null;
        if (this.FunctionTreeSelector.FunctionSelected)
        {
            tools = this.FunctionGroupSource?.GetFunctionGroups().OfType<CheckableFunctionGroupTree>().ToArray();
        }

        var ragSources = RagSources.Where(model => model is { IsSelected: true, Data.IsAvailable: true })
            .Select(model => model.Data)
            .ToArray();
        //每次搜索的条件可能不同，所以传递的是副本
        return new RequestViewItem(promptBuilder.ToString())
        {
            Attachments = Attachments.Count == 0 ? null : Attachments.ToList(),
            FunctionGroups = tools == null ? [] : [..tools],
            SearchOption = SearchConfig.GetUserSearchOption(),
            RagSources = ragSources.Length > 0 ? ragSources : null,
            CallEngine = this.FunctionTreeSelector.EngineType ??
                         this.FunctionTreeSelector.SelectableCallEngineTypes.FirstOrDefault(),
        };
    }

    private void TagDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    protected virtual void OnRequestCompleted(IResponse obj)
    {
        RequestCompleted?.Invoke(obj);
    }
}