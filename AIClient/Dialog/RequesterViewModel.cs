using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.MCP;
using LLMClient.MCP.Servers;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
            
            var completedResult = await _getResponse.Invoke(this.DefaultClient, requestViewItem, null);
            OnRequestCompleted(completedResult);
            if (!completedResult.IsInterrupt)
            {
                ClearRequest();
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
    });

    public ICommand SummarizeCommand => new ActionCommand(_ => { Summarize(); });

    public ICommand ChangeModelCommand => new ActionCommand(async o =>
    {
        var selectionViewModel = new ModelSelectionPopupViewModel();
        if (await DialogHost.Show(selectionViewModel) is true)
        {
            var model = selectionViewModel.GetClient();
            if (model == null)
            {
                MessageBox.Show("No model created!");
                return;
            }

            this.DefaultClient = model;
        }
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

            _defaultClient.FunctionInterceptor = FunctionAuthorizationInterceptor.Instance;
            _defaultClient = value;
            BindClient(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThinkingAvailable));
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
        new ObservableCollection<Attachment>();

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

    public ThinkingConfigViewModel ThinkingConfig { get; }

    public bool ThinkingAvailable
    {
        get
        {
            if (!this.DefaultClient.Model.Reasonable)
            {
                return false;
            }

            return this.ThinkingConfig.Config != null;
        }
    }

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
        }
    }

    #endregion

    private readonly Func<ILLMChatClient, IRequestItem, int?, Task<CompletedResult>> _getResponse;
    private readonly GlobalOptions _options;

    public RequesterViewModel(ILLMChatClient modelClient,
        Func<ILLMChatClient, IRequestItem, int?, Task<CompletedResult>> getResponse, GlobalOptions options,
        IRagSourceCollection ragSourceCollection, IMapper mapper)
    {
        FunctionTreeSelector = new AIFunctionTreeSelectorViewModel();
        SearchConfig = new SearchConfigViewModel();
        ThinkingConfig = new ThinkingConfigViewModel();
        QueryViewModel = new FileQueryViewModel(this);
        this._defaultClient = modelClient;
        this._getResponse = getResponse;
        _options = options;
        _ragSourceCollection = ragSourceCollection;
        _mapper = mapper;
        this.BindClient(modelClient);
        this.PropertyChanged += (sender, args) => { this.IsDataChanged = true; };
    }

    private readonly FunctionAuthorizationInterceptor _authorizationInterceptor = new FunctionAuthorizationInterceptor()
    {
        Filters = { new CommandAuthorization() }
    };

    private readonly IMapper _mapper;

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

        client.FunctionInterceptor = _authorizationInterceptor;
        this.SearchConfig.ResetSearch(client);
        this.ThinkingConfig.ResetConfig(client);
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

    public IFunctionGroupSource? Source { get; set; }

    public async void Summarize(int? index = null)
    {
        try
        {
            var summaryRequest = new SummaryRequestViewItem()
            {
                SummaryPrompt = _options.TokenSummarizePrompt,
                OutputLength = _options.SummarizeWordsCount,
                InteractionId = Guid.NewGuid(),
            };
            var summarizeModel = _options.SummarizeClient ?? this.DefaultClient;
            await _getResponse.Invoke(summarizeModel, summaryRequest, index);
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
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
            tools = this.Source?.GetFunctionGroups().OfType<CheckableFunctionGroupTree>().ToArray();
        }

        var thinkingConfig = this.ThinkingConfig.Enable ? this.ThinkingConfig.Config : null;
        if (thinkingConfig != null)
        {
            _mapper.Map<IThinkingConfig, IThinkingConfig>(this.ThinkingConfig,
                thinkingConfig);
        }

        var ragSources = RagSources.Where(model => model is { IsSelected: true, Data.IsAvailable: true })
            .Select(model => model.Data)
            .ToArray();
        //每次搜索的条件可能不同，所以传递的是副本
        return new RequestViewItem()
        {
            InteractionId = Guid.NewGuid(),
            TextMessage = promptBuilder.ToString().Trim(),
            Attachments = Attachments.ToList(),
            FunctionGroups = tools == null ? [] : [..tools],
            SearchOption = SearchConfig.GetUserSearchOption(),
            ThinkingConfig = thinkingConfig,
            RagSources = ragSources.Length > 0 ? ragSources : null,
            CallEngine = this.FunctionTreeSelector.EngineType,
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

public class CommandAuthorization : IFunctionAuthorizationFilter
{
    public bool Matches(FunctionCallContent functionCall)
    {
        return functionCall.Name == "WinCLI_ExecuteCommand";
    }

    public Task<bool> AuthorizeAsync(FunctionCallContent functionCall, CancellationToken cancellationToken)
    {
        return Task.FromResult(MessageBox.Show(
            $"Function call {functionCall.Name} is requested, parameters: {functionCall.GetDebuggerString()}.\r\n permmit?",
            "Function Call Request", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
    }
}