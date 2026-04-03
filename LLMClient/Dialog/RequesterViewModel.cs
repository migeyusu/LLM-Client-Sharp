using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.UserControls;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using LLMClient.Agent;
using LLMClient.Agent.MiniSWE;
using LLMClient.Workflow.Research;

namespace LLMClient.Dialog;

public class RequestOption
{
    public required ILLMChatClient DefaultClient { get; init; }

    public required IRequestItem RequestItem { get; init; }

    public AgentDescriptor? Agent { get; set; }

    public bool UseAgent { get; set; }

    public required AgentOption AgentOption { get; init; }
}

public delegate Task<IResponse> GetResponseHandler(RequestOption request,
    IRequestItem? insertBefore, CancellationToken token);

public record AgentDescriptor(string Name, Type Type);

public class RequesterViewModel : BaseViewModel, IChatRequest
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = true;

    private AgentOption _agentOption = new();

    public AgentOption AgentOption
    {
        get => _agentOption;
        set
        {
            if (ReferenceEquals(value, _agentOption)) return;
            _agentOption.PropertyChanged -= TagDataChanged;
            _agentOption = value;
            _agentOption.PropertyChanged += TagDataChanged;
            OnPropertyChanged();
        }
    }

    public event Action<IResponse>? RequestCompleted;

    public ICommand NewRequestCommand => new ActionCommand(async o =>
    {
        try
        {
            var request = await this.CreateRequest();
            if (request == null)
            {
                return;
            }

            IsNewResponding = true;
            using (_tokenSource = new CancellationTokenSource())
            {
                var completedResult =
                    await _getResponse.Invoke(new RequestOption()
                    {
                        Agent = this.SelectedAgent,
                        DefaultClient = DefaultClient,
                        RequestItem = request,
                        UseAgent = this.IsAgentMode,
                        AgentOption = this.AgentOption
                    }, null, _tokenSource.Token);
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

    public bool RawEditMode
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            RefreshEditor(value);
        }
    }

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

    //不需要持久化
    public IList<SelectableViewModel<IRagSource>> SelectableRagSources
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = Array.Empty<SelectableViewModel<IRagSource>>();

    public void NotifyRagSelection()
    {
        OnPropertyChanged(nameof(IsRagEnabled));
    }

    public bool IsRagEnabled => SelectableRagSources.Any(model => model.IsSelected && model.Data.IsAvailable);

    public void RefreshRagSources()
    {
        var selected = SelectableRagSources.Where(model => model.IsSelected).ToList();
        var selectableViewModels = _ragSourceCollection.Sources.ToSelectable().ToArray();
        foreach (var selectableViewModel in selectableViewModels)
        {
            var dataId = selectableViewModel.Data.Id;
            if (selected.Find((model => model.Data.Id == dataId)) != null)
            {
                selectableViewModel.IsSelected = true;
            }
        }

        SelectableRagSources = selectableViewModels;
    }

    public FileQueryViewModel QueryViewModel { get; }

    #endregion

    #region attachment

    /// <summary>
    /// 这里的Attachment不会持久化，只有发出之后的请求中附带的attchment会被持久化（缓存）。
    /// </summary>
    public ObservableCollection<Attachment> Attachments { get; set; } =
        new();

    public ICommand AddImageCommand { get; }

    public ICommand RemoveAttachmentCommand { get; }

    #endregion

    #region function call

    public AIFunctionTreeSelectorViewModel FunctionTreeSelector { get; }

    #endregion

    public SearchConfigViewModel SearchConfig { get; }

    #region input

    public TextContentEditViewModel PromptEditViewModel
    {
        get => _promptEditViewModel;
        private set
        {
            if (Equals(value, _promptEditViewModel)) return;
            _promptEditViewModel = value;
            OnPropertyChanged();
        }
    }

    public long EstimatedTokens
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                var contentText = PromptEditViewModel.FinalText;
                if (string.IsNullOrEmpty(contentText))
                {
                    return 0;
                }

                return await _tokensCounter.CountTokens(contentText);
            });
        }
    }

    public ICommand CancelLastCommand { get; }

    public ICommand OpenExpandedEditorCommand { get; }

    private bool _isDebugMode = true;

    #region ichatrequest

    public string? UserPrompt
    {
        get { return PromptEditViewModel.FinalText; }
    }

    public ISearchOption? SearchOption
    {
        get { return SearchConfig.GetUserSearchOption(); }
    }

    public List<CheckableFunctionGroupTree>? FunctionGroups
    {
        get
        {
            if (this.FunctionTreeSelector.IsFunctionEnabled)
            {
                return this.FunctionGroupSource?
                    .GetFunctionGroups()
                    .OfType<CheckableFunctionGroupTree>()
                    .ToList();
            }

            return null;
        }
    }

    public IRagSource[]? RagSources
    {
        get
        {
            var ragSources = SelectableRagSources.Where(model => model is { IsSelected: true, Data.IsAvailable: true })
                .Select(model => model.Data)
                .ToArray();
            return ragSources.Length > 0 ? ragSources : null;
        }
    }

    public ChatResponseFormat? ResponseFormat { get; } = null;

    public FunctionCallEngineType CallEngineType
    {
        get
        {
            return this.FunctionTreeSelector.EngineType ??
                   this.FunctionTreeSelector.SelectableCallEngineTypes.FirstOrDefault();
        }
    }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; } = null;

    #endregion

    public bool IsDebugMode
    {
        get => _isDebugMode;
        set
        {
            if (value == _isDebugMode) return;
            _isDebugMode = value;
            OnPropertyChanged();
        }
    }

    public bool AutoApproveAllInvocations
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
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

    private bool _isAgentMode;

    public bool IsAgentMode
    {
        get => _isAgentMode;
        set
        {
            if (value == _isAgentMode) return;
            _isAgentMode = value;
            OnPropertyChanged();
        }
    }

    public List<AgentDescriptor> AvailableAgents { get; } = [];

    private AgentDescriptor? _selectedAgent;

    public AgentDescriptor? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (Equals(value, _selectedAgent)) return;
            _selectedAgent = value;
            OnPropertyChanged();
        }
    }

    #endregion

    private readonly GetResponseHandler _getResponse;

    private readonly GlobalOptions _options;
    private readonly Summarizer _summarizer;

    private readonly ITokensCounter _tokensCounter;

    public RequesterViewModel(string initialPrompt, ILLMChatClient modelClient,
        GetResponseHandler getResponse,
        GlobalOptions options, Summarizer summarizer, IRagSourceCollection ragSourceCollection,
        ITokensCounter tokensCounter)
    {
        FunctionTreeSelector = new AIFunctionTreeSelectorViewModel();
        SearchConfig = new SearchConfigViewModel();
        _promptEditViewModel = new TextContentCodeEditViewModel(new TextContent(initialPrompt), null);
        QueryViewModel = new FileQueryViewModel(this);
        this._defaultClient = modelClient;
        this._getResponse = getResponse;
        _options = options;
        _summarizer = summarizer;
        _ragSourceCollection = ragSourceCollection;
        _tokensCounter = tokensCounter;
        _agentOption.PropertyChanged += TagDataChanged;
        this.BindClient(modelClient);

        // Initialize agents
        List<Type> agentTypes = new List<Type> { typeof(MiniSweAgent), typeof(NvidiaResearchClient) };
#if DEBUG
        agentTypes.Add(typeof(TestSuccessAgent));
        agentTypes.Add(typeof(TestFailedAgent));
#endif

        foreach (var type in agentTypes)
        {
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? type.Name;
            AvailableAgents.Add(new AgentDescriptor(description, type));
        }

        SelectedAgent = AvailableAgents.FirstOrDefault();

        CancelLastCommand = new ActionCommand(_ => { _tokenSource?.Cancel(); });
        OpenExpandedEditorCommand = new ActionCommand(async o =>
        {
            if (o is RequesterViewModel vm)
            {
                await DialogHost.Show(vm.PromptEditViewModel);
                // Persist edits made inside the dialog back to Content.Text
                await vm.PromptEditViewModel.ApplyText();
                // Recreate the ViewModel so the main RichTextBox gets a fresh
                // FlowDocument — avoids the "stolen document" issue where WPF
                // binding skips OnDocumentChanged because the same object reference
                // is returned and the main RTB never re-acquires ownership.
                vm.RecreatePromptEditor();
                vm.InvalidateAsyncProperty(nameof(EstimatedTokens));
            }
        });
        AddImageCommand = new ActionCommand(_ =>
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
        RemoveAttachmentCommand = new ActionCommand(o =>
        {
            if (o is Attachment attachment)
            {
                this.Attachments.Remove(attachment);
            }
        });
        this.PropertyChanged += (_, args) =>
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

    private async void RefreshEditor(bool rawMode)
    {
        if (rawMode && PromptEditViewModel is TextContentCodeEditViewModel codeVm)
        {
            await codeVm.ApplyText();
            PromptEditViewModel = new TextContentRawEditViewModel(new TextContent(codeVm.FinalText),
                codeVm.MessageId);
        }
        else if (!rawMode && PromptEditViewModel is TextContentRawEditViewModel rawVm)
        {
            await rawVm.ApplyText();
            PromptEditViewModel = new TextContentCodeEditViewModel(new TextContent(rawVm.FinalText),
                rawVm.MessageId);
        }
    }

    public void ClearRequest()
    {
        this.Attachments.Clear();
        var textContent = new TextContent(string.Empty);
        this.PromptEditViewModel = this.RawEditMode
            ? new TextContentRawEditViewModel(textContent, null)
            : new TextContentCodeEditViewModel(textContent, null);
        InvalidateAsyncProperty(nameof(EstimatedTokens));
    }

    /// <summary>
    /// Recreates PromptEditViewModel with the current text so the main RichTextBox
    /// receives a brand-new FlowDocument and fully re-renders its content.
    /// </summary>
    private void RecreatePromptEditor()
    {
        var text = PromptEditViewModel.FinalText;
        var messageId = PromptEditViewModel.MessageId;
        PromptEditViewModel = RawEditMode
            ? new TextContentRawEditViewModel(new TextContent(text), messageId)
            : new TextContentCodeEditViewModel(new TextContent(text), messageId);
    }


    private CancellationTokenSource? _tokenSource;
    private TextContentEditViewModel _promptEditViewModel;

    public async void Summarize(IRequestItem? insertBefore = null)
    {
        IsNewResponding = true;
        var summaryRequest = _summarizer.CreateContextSummarizeRequest();
        try
        {
            var summarizeModel = _options.CreateContextSummarizeClient() ?? this.DefaultClient;
            using (_tokenSource = new CancellationTokenSource())
            {
                await _getResponse.Invoke(new RequestOption()
                {
                    DefaultClient = summarizeModel,
                    UseAgent = false,
                    RequestItem = summaryRequest,
                    AgentOption = AgentOption
                }, insertBefore, _tokenSource.Token);
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
        finally
        {
            summaryRequest.IsSummarizing = false;
            IsNewResponding = false;
        }
    }

    public async Task<RequestViewItem?> CreateRequest()
    {
        if (!await PromptEditViewModel.ApplyAndCheck())
        {
            return null;
        }

        //每次搜索的条件可能不同，所以传递的是副本
        var requestViewItem = new RequestViewItem(PromptEditViewModel.FinalText)
        {
            Attachments = Attachments.Count == 0 ? null : Attachments.ToList(),
        };
        DefaultDialogContextBuilder.IChatRequestMapper.Map<IChatRequest, RequestViewItem>(this, requestViewItem);
        await requestViewItem.EnsureInitializeAsync();
        return requestViewItem;
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