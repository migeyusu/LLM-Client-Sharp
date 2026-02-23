// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Dialog.Models;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public class DialogViewModel : DialogSessionViewModel, IFunctionGroupSource, IPromptableSession
{
    //创建新实例后默认为changed
    private bool _isDataChanged = true;

    public override bool IsDataChanged
    {
        get { return _isDataChanged || Requester.IsDataChanged; }
        set
        {
            _isDataChanged = value;
            Requester.IsDataChanged = value;
        }
    }

    public override string? Name
    {
        get { return this.Topic; }
        set { this.Topic = value ?? "新建会话"; }
    }

    private string? _userSystemPrompt;

    public string? UserSystemPrompt
    {
        get => _userSystemPrompt;
        set
        {
            if (value == _userSystemPrompt) return;
            _userSystemPrompt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }

    private ObservableCollection<PromptEntry> _extendedSystemPrompts = [];

    public ObservableCollection<PromptEntry> ExtendedSystemPrompts
    {
        get => _extendedSystemPrompts;
        set
        {
            if (Equals(value, _extendedSystemPrompts)) return;
            _extendedSystemPrompts.CollectionChanged -= ExtendedSystemPromptsOnCollectionChanged;
            _extendedSystemPrompts = value;
            value.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SystemPrompt));
        }
    }

    public override string? SystemPrompt
    {
        get
        {
            if (!ExtendedSystemPrompts.Any() && string.IsNullOrEmpty(UserSystemPrompt))
            {
                return null;
            }

            var stringBuilder = new StringBuilder();
            foreach (var promptEntry in ExtendedSystemPrompts)
            {
                stringBuilder.AppendLine(promptEntry.Prompt);
            }

            stringBuilder.AppendLine(UserSystemPrompt);
            return stringBuilder.ToString();
        }
    }

    private string _topic;
    private readonly GlobalOptions _options;

    public string Topic
    {
        get => _topic;
        set
        {
            if (value == _topic) return;
            _topic = value;
            OnPropertyChangedAsync();
        }
    }

    #region request chain

    public bool IsChaining
    {
        get => _isChaining;
        set
        {
            if (value == _isChaining) return;
            _isChaining = value;
            OnPropertyChanged();
        }
    }

    public int ChainStepCount
    {
        get => _chainStepCount;
        set
        {
            if (value == _chainStepCount) return;
            _chainStepCount = value;
            OnPropertyChanged();
        }
    }

    public int ChainingStep
    {
        get => _chainingStep;
        set
        {
            if (value == _chainingStep) return;
            _chainingStep = value;
            OnPropertyChanged();
        }
    }

    #endregion

    private bool _isChaining;
    private int _chainStepCount;
    private int _chainingStep;

    private readonly string[] _notTrackingProperties =
    [
        nameof(ScrollViewItem),
        nameof(SearchText)
    ];

    public RequesterViewModel Requester { get; }

    public DialogViewModel(string topic, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IViewModelFactory factory, IDialogItem? rootNode = null, IDialogItem? currentLeaf = null)
        : base(rootNode, currentLeaf)
    {
        _topic = topic;
        _options = options;
        ((INotifyCollectionChanged)this.RootNode.Children).CollectionChanged += OnRootCollectionChanged;
        Requester = factory.CreateViewModel<RequesterViewModel>(modelClient,
            (Func<ILLMChatClient, IRequestItem, IRequestItem?, CancellationToken, Task<CompletedResult>>)
            NewResponse);
        Requester.FunctionGroupSource = this;
        Requester.FunctionTreeSelector.Reset();
        var functionTreeSelector = Requester.FunctionTreeSelector;
        functionTreeSelector.ConnectDefault()
            .ConnectSource(new ProxyFunctionGroupSource(() => this.SelectedFunctionGroups));
        functionTreeSelector.AfterSelect += FunctionTreeSelectorOnAfterSelect;
        PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (_notTrackingProperties.Contains(propertyName))
            {
                return;
            }

            IsDataChanged = true;
        };
        _extendedSystemPrompts.CollectionChanged += ExtendedSystemPromptsOnCollectionChanged;
    }

    private void ExtendedSystemPromptsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        OnPropertyChanged(nameof(SystemPrompt));
    }

    private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_options.EnableAutoSubjectGeneration &&
            e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset
            && this.DialogItems.Count == 0)
        {
            this.Topic = "新建会话";
        }
    }

    private static readonly TimeSpan TopicTimeOut = TimeSpan.FromSeconds(30);

    public Task? SummarizeTask = null;

    public override async Task<CompletedResult> InvokeRequest(ResponseViewItem responseViewItem,
        MultiResponseViewItem multiResponseViewItem)
    {
        var completedResult = await base.InvokeRequest(responseViewItem, multiResponseViewItem);
        //判断是否需要进行主题总结
        if (this.Topic == "新建会话" &&
            this.DialogItems.FirstOrDefault(item => item is MultiResponseViewItem) == multiResponseViewItem
            && !completedResult.IsInterrupt
            && _options.EnableAutoSubjectGeneration
            && (SummarizeTask == null || SummarizeTask.IsCompleted))
        {
            //不要wait
            SummarizeTask = Task.Run(async () =>
            {
                var summarizer = new Summarizer(_options);
                var newTopic = await summarizer.SummarizeTopicAsync(this, TopicTimeOut);
                if (!string.IsNullOrEmpty(newTopic))
                {
                    this.Topic = newTopic;
                }
            });
        }

        return completedResult;
    }

    private void FunctionTreeSelectorOnAfterSelect()
    {
        this.SelectedFunctionGroups =
            this.Requester.FunctionTreeSelector.FunctionGroups.Where(tree => tree.IsSelected != false).ToArray();
        PopupBox.ClosePopupCommand.Execute(null, null);
    }

    public IEnumerable<IAIFunctionGroup> GetFunctionGroups()
    {
        if (SelectedFunctionGroups == null)
        {
            yield break;
        }

        foreach (var functionGroupTree in SelectedFunctionGroups)
        {
            functionGroupTree.RefreshCheckState();
            if (functionGroupTree.IsSelected != false)
            {
                yield return functionGroupTree;
            }
        }
    }
}