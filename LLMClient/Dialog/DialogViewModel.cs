// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using AutoMapper;
using DocumentFormat.OpenXml.InkML;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using LLMClient.ToolCall.Servers;
using MaterialDesignThemes.Wpf;
using MessageBox = System.Windows.MessageBox;
using Trace = System.Diagnostics.Trace;

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

    public async void SequentialChain(IEnumerable<IDialogItem> dialogItems)
    {
        var client = Requester.DefaultClient;
        RespondingCount++;
        IsChaining = true;
        ChainingStep = 0;
        var pendingItems = dialogItems
            .Where(item => item is IRequestItem || item is EraseViewItem)
            .ToArray();
        ChainStepCount = pendingItems.Length;
        try
        {
            foreach (var oldDialogDialogItem in pendingItems)
            {
                if (oldDialogDialogItem is IRequestItem requestViewItem)
                {
                    var newGuid = Guid.NewGuid();
                    var newItem = Extension.Clone(requestViewItem);
                    DialogItems.Add(newItem);
                    var copy = GenerateHistoryFromSelf();
                    int retryCount = 3;
                    while (retryCount > 0)
                    {
                        var multiResponseViewItem = new MultiResponseViewItem(this) { InteractionId = newGuid };
                        DialogItems.Add(multiResponseViewItem);
                        var completedResult =
                            await AddNewResponse(client, copy, multiResponseViewItem, this.UserSystemPrompt);
                        if (!completedResult.IsInterrupt)
                        {
                            break;
                        }

                        DialogItems.Remove(multiResponseViewItem);
                        retryCount--;
                    }

                    if (retryCount == 0)
                    {
                        MessageBox.Show("请求失败，重试次数已用完！", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else if (oldDialogDialogItem is EraseViewItem)
                {
                    DialogItems.Add(new EraseViewItem());
                }

                ChainingStep++;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show("重试处理对话失败: " + exception.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RespondingCount--;
            IsChaining = false;
            /*this.ChainStepCount = 0;
            this.ChainingStep = 0;*/
            ScrollViewItem = DialogItems.Last();
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

    private IList<CheckableFunctionGroupTree>? _selectedFunctionGroups;

    public RequesterViewModel Requester { get; }

    public IList<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get => _selectedFunctionGroups;
        set
        {
            if (Equals(value, _selectedFunctionGroups)) return;
            _selectedFunctionGroups = value;
            OnPropertyChanged();
        }
    }

    public DialogViewModel(string topic, ILLMChatClient modelClient, IMapper mapper,
        GlobalOptions options, IRagSourceCollection ragSourceCollection,
        IList<IDialogItem>? items = null) : base(mapper, items)
    {
        _topic = topic;
        _options = options;
        Requester = new RequesterViewModel(modelClient, NewRequest, options, ragSourceCollection)
        {
            FunctionGroupSource = this
        };
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

    protected override void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        base.DialogOnCollectionChanged(sender, e);
        if (_options.EnableAutoSubjectGeneration &&
            e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset
            && this.DialogItems.Count == 0)
        {
            this.Topic = "新建会话";
        }
    }

    private static readonly TimeSpan TopicTimeOut = TimeSpan.FromSeconds(30);

    public override async Task<CompletedResult> InvokeRequest(Func<Task<CompletedResult>> invoke)
    {
        //判断是否需要进行主题总结
        var needSummarize = this.DialogItems is [IRequestItem, MultiResponseViewItem]
                            && this.Topic == "新建会话";

        var completedResult = await base.InvokeRequest(invoke);
        if (needSummarize && !completedResult.IsInterrupt
                          && _options.EnableAutoSubjectGeneration)
        {
            //不要wait
            _ = Task.Run(async () =>
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