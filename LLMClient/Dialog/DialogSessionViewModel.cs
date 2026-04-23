using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Controls;
using LLMClient.Dialog.Models;
using LLMClient.Agent;
using LLMClient.Agent.Inspector;
using LLMClient.Agent.MiniSWE;
using LLMClient.Agent.Planner;
using LLMClient.Agent.Research;
using LLMClient.Configuration;
using LLMClient.ToolCall;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using MaterialDesignThemes.Wpf;
using Microsoft.Agents.AI;

namespace LLMClient.Dialog;

public abstract class DialogSessionViewModel : NotifyDataErrorInfoViewModelBase,
    IDialogGraphViewModel, ITextDialogSession, IFunctionGroupSource
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public virtual bool IsDataChanged { get; set; } = true;

    public string? Shortcut
    {
        get
        {
            var textContent = VisualDialogItems.OfType<RequestViewItem>()
                .FirstOrDefault(item => item.IsAvailableInContext)
                ?.RawTextMessage;
            return string.IsNullOrEmpty(textContent)
                ? null
                : textContent?.Substring(0, int.Min(20, textContent.Length));
        }
    }

    /// <summary>
    /// 正在处理
    /// </summary>
    public bool IsBusy
    {
        get => RespondingCount > 0;
    }

    public int RespondingCount
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public long TokensConsumption
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public double TotalPrice
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IList<CheckableFunctionGroupTree>? SelectedFunctionGroups
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public virtual IFunctionGroupSource? ToolsSource => this;

    #region scroll

    public ParallelResponseViewItem? CurrentParallelResponseViewItem
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IDialogItem? ScrollViewItem
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            if (value is ISearchableDialogItem viewItem)
            {
                viewItem.SearchableDocument?.EnsureSearch();
            }

            CurrentParallelResponseViewItem = value as ParallelResponseViewItem;
        }
    }

    #endregion

    #region search

    public bool IsSearchVisible
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleSearchCommand { get; }

    private string? _highlightedText;

    private string? _searchText;

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (value == _searchText) return;
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public ICommand SearchCommand { get; }

    private int _currentHighlightIndex = 0;

    private ISearchableDialogItem? FocusedHighlightItem
    {
        get;
        set
        {
            field = value;
            var document = value?.SearchableDocument;
            if (document is { HasMatched: true })
            {
                document.EnsureSearch();
            }
        }
    }

    SearchableDocument? FocusedHighlightDocument
    {
        get { return FocusedHighlightItem?.SearchableDocument; }
    }

    private void CheckFocusItem(IList<ISearchableDialogItem> searchableDialogItems, ref int itemIndex)
    {
        if (FocusedHighlightItem != null)
        {
            itemIndex = searchableDialogItems.IndexOf(FocusedHighlightItem);
            if (itemIndex == -1)
            {
                FocusedHighlightItem = null;
                itemIndex = 0;
            }
        }

        if (FocusedHighlightItem == null)
        {
            this.FocusedHighlightItem = searchableDialogItems.First();
        }
    }

    private void GoToHighlight()
    {
        ScrollViewItem = this.FocusedHighlightItem;
        var count = FocusedHighlightDocument?.FoundTextRanges.Count;
        if (count == null || count == 0)
        {
            return;
        }

        if (_currentHighlightIndex < 0)
        {
            _currentHighlightIndex = 0;
        }
        else if (_currentHighlightIndex >= count)
        {
            _currentHighlightIndex = 0;
        }

        var foundTextRange = FocusedHighlightDocument?.FoundTextRanges[_currentHighlightIndex];
        if (foundTextRange == null)
            return;
        var parent = FocusedHighlightDocument?.Document.Parent;
        if (parent is FlowDocumentScrollViewerEx ex)
        {
            ex.ScrollToRange(foundTextRange);
        }
    }

    public ICommand GoToNextHighlightCommand { get; }

    public ICommand GoToPreviousHighlightCommand { get; }

    public ICommand SetLeafCommand { get; }

    #endregion

    #region items management

    public List<IChatHistoryItem> GetHistory()
    {
        if (this.CurrentLeaf is IResponseItem responseItem)
        {
            return responseItem.GetChatHistory()
                .OfType<IChatHistoryItem>()
                .ToList();
        }

        return [];
    }

    public abstract string? SystemPrompt { get; }

    public IDialogItem RootNode { get; }

    private IDialogItem _currentLeaf;

    /// <summary>
    /// 当前叶子节点，用于新建请求时定位上下文，默认是最后一个可用节点
    /// <para>用法：新建分支时设置改节点为最后一个节点</para>
    /// </summary>
    public IDialogItem CurrentLeaf
    {
        get => _currentLeaf;
        set
        {
            if (Equals(value, _currentLeaf)) return;
            _currentLeaf = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentContextTokens));
            RebuildLinearItems();
        }
    }

    private void RebuildLinearItems()
    {
        var newList = CurrentLeaf.PathFromRoot();
        ObservableCollectionPatcher.PatchByLcs(DialogItemsObservable, newList,
            a => a.Id);
        // DialogItemsObservable.ResetWith(newList);
    }

    public SuspendableObservableCollection<IDialogItem> DialogItemsObservable { get; } = [];

    private readonly ReadOnlyObservableCollection<IDialogItem> _readOnlyDialogItems;

    public bool IsNodeSelectable(IDialogItem item)
    {
        return item is IResponseItem;
    }

    public Guid ID { get; set; } = Guid.NewGuid();

    public IReadOnlyList<IDialogItem> VisualDialogItems
    {
        get { return _readOnlyDialogItems; }
    }

    public IResponseItem WorkingResponse => CurrentLeaf as IResponseItem ?? throw new InvalidOperationException("当前节点不是回复项");

    public ICommand ClearContextCommand { get; }

    public ICommand OpenDialogRouteCommand { get; }

    /// <summary>
    /// 用于子项路由选择
    /// </summary>
    public DialogGraphViewModel SharedGraphViewModel { get; }

    private readonly Type[] _supportedAgents = [typeof(MiniSweAgent), typeof(NvidiaResearchClient)];

    public virtual IEnumerable<Type> SupportedAgents => _supportedAgents;

    /// <summary>
    /// 通过插入擦除标记来切断上下文
    /// </summary>
    /// <param name="requestViewItem">if null:last</param>
    public void CutContext(IRequestItem? requestViewItem = null)
    {
        if (VisualDialogItems.Count == 0)
        {
            return;
        }

        var eraseViewItem = new EraseViewItem();
        if (requestViewItem != null)
        {
            var indexOf = VisualDialogItems.IndexOf(requestViewItem);
            if (indexOf <= 0)
            {
                Trace.TraceWarning("请求项不在对话列表中，无法切断上下文");
                return;
            }

            requestViewItem.InsertBefore(eraseViewItem);
        }
        else
        {
            CurrentLeaf.AppendChild(eraseViewItem);
            CurrentLeaf = eraseViewItem;
        }

        RebuildLinearItems();
        ScrollViewItem = eraseViewItem;
    }

    public Task CutContextAsync(IRequestItem? requestItem = null)
    {
        return DispatchAsync(() => CutContext(requestItem));
    }

    public abstract AIContextProvider[]? ContextProviders { get; }

    public ICommand ClearDialogCommand { get; }

    public void DeleteInteraction(IRequestItem preRequest)
    {
        //删除单次交互，要求：1. 请求没有多回复分支 2. 回复没有多后继分支（允许单后继）
        if (preRequest.HasFork)
        {
            MessageBoxes.Warning("无法删除，存在多回复分支", "提示");
            return;
        }

        if (preRequest.Children.First() is not IResponseItem preResponse)
        {
            return;
        }

        if (preResponse.HasFork)
        {
            MessageBoxes.Warning("无法删除，回复存在多后继分支", "提示");
            return;
        }

        var previousItem = preRequest.PreviousItem;
        if (previousItem == null)
        {
            return;
        }

        previousItem.RemoveChild(preRequest);
        var nextRequest = preResponse.Children.FirstOrDefault();
        if (nextRequest != null)
        {
            previousItem.AppendChild(nextRequest);
        }

        //检查leaf可达性
        if (!CurrentLeaf.CanReachRoot())
        {
            CurrentLeaf = previousItem;
        }
        else
        {
            RebuildLinearItems();
        }

        this.IsDataChanged = true;
    }

    public virtual void DeleteItem(IDialogItem item)
    {
        var oriPreviousItem = item.PreviousItem;
        switch (item)
        {
            case IRequestItem requestItem:
                DeleteChain(requestItem);
                break;
            case EraseViewItem eraseViewItem:
                eraseViewItem.Delete();
                break;
        }

        if (this.VisualDialogItems.Contains(item))
        {
            //检查leaf可达性
            if (!CurrentLeaf.CanReachRoot())
            {
                CurrentLeaf = oriPreviousItem ?? RootNode;
            }
            else
            {
                RebuildLinearItems();
            }
        }

        this.IsDataChanged = true;
    }

    /// <summary>
    /// 删除整个请求链（请求及其所有响应），如果存在分叉则提示是否删除整个节点树
    /// <para>由于多种分叉情况，理想状态下的将后继节点连接到前回复是困难且会丢失语义的</para>
    /// </summary>
    /// <param name="requestItem"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void DeleteChain(IRequestItem requestItem)
    {
        var previousItem = requestItem.PreviousItem;
        if (previousItem == null)
        {
            throw new InvalidOperationException("PreviousItem 不能为空");
        }

        if (IsRunning(requestItem))
        {
            MessageBoxes.Warning("当前节点后续存在正在回复的内容，无法删除", "提示");
            return;
        }

        if (requestItem.HasFork)
        {
            if (!MessageBoxes.Question("存在多个分叉，是否要删除整个节点树？", "提示"))
            {
                return;
            }
        }
        else
        {
            if (GetDepth(requestItem) > 2)
            {
                if (!MessageBoxes.Question("当前节点后包含较长对话，是否要删除整个节点树？", "提示"))
                {
                    return;
                }
            }
        }

        previousItem.RemoveChild(requestItem);
        return;

        int GetDepth(IDialogItem item)
        {
            if (item.Children.Count == 0)
            {
                return 1;
            }

            return 1 + item.Children.Max(GetDepth);
        }

        bool IsRunning(IDialogItem item)
        {
            return item is IResponseItem { IsResponding: true } || item.Children.Any(IsRunning);
        }
    }

    #endregion

    public long CurrentContextTokens
    {
        get
        {
            if (!VisualDialogItems.Any())
            {
                return 0;
            }

            try
            {
                if (CurrentLeaf is IResponseItem responseViewItem)
                {
                    return responseViewItem.GetChatHistory().Append(CurrentLeaf).Sum(item => item.Tokens);
                }

                return 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public virtual string? Topic
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    } = "新建会话";

    public ICommand ExportCommand { get; }

    #region core methods

    public virtual Task OnPreviewRequest(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    private static readonly TimeSpan TopicTimeOut = TimeSpan.FromSeconds(30);

    public Task? SummarizeTask = null;

    private readonly Summarizer _summarizer;

    public virtual void OnResponseCompleted(IResponse response)
    {
        this.TokensConsumption += response.Usage?.TotalTokenCount ?? 0;
        this.TotalPrice += response.Price ?? 0;
        //判断是否需要进行主题总结
        if (this.Topic == "新建会话"
            /*&& this.DialogItems.OfType<IResponseItem>().Count() == 1*/
            && !response.IsInterrupt
            && _options.EnableAutoSubjectGeneration
            && (SummarizeTask == null || SummarizeTask.IsCompleted))
        {
            //不要wait
            SummarizeTask = Task.Run(async () =>
            {
                var newTopic = await _summarizer.SummarizeSessionTopicAsync(this, TopicTimeOut);
                if (!string.IsNullOrEmpty(newTopic))
                {
                    this.Topic = newTopic;
                }
            }, CancellationToken.None);
        }
    }

    public async Task<IResponse> NewResponse(RequestOption option,
        IRequestItem? insertBefore = null, CancellationToken token = default)
    {
        var client = option.DefaultClient;
        var requestViewItem = option.RequestItem;
        if (option.UseAgent)
        {
            var agentType = option.Agent?.Type ?? throw new InvalidOperationException("未指定Agent类型");
            return await ExecuteAgentAsync(client, requestViewItem,
                agentType, option.AgentOption, insertBefore, token);
        }

        var multiResponseViewItem = new ParallelResponseViewItem(this)
            { InteractionId = requestViewItem.InteractionId };
        InsertResponseItem(requestViewItem, insertBefore, multiResponseViewItem);
        return await multiResponseViewItem.NewResponse(client, token);
    }

    private void InsertResponseItem(IRequestItem requestViewItem, IRequestItem? insertBefore, IDialogItem responseItem)
    {
        if (insertBefore == null)
        {
            this.CurrentLeaf.AppendChild(requestViewItem)
                .AppendChild(responseItem);
            this.CurrentLeaf = responseItem;
        }
        else
        {
            var previousItem = insertBefore.PreviousItem;
            if (previousItem == null)
            {
                throw new InvalidOperationException("无法在根节点前插入请求");
            }

            previousItem.RemoveChild(insertBefore);
            previousItem.AppendChild(requestViewItem)
                .AppendChild(responseItem)
                .AppendChild(insertBefore);
            RebuildLinearItems();
        }

        this.ScrollViewItem = responseItem;
    }

    private async Task<IResponse> ExecuteAgentAsync(ILLMChatClient client, IRequestItem requestViewItem, Type agentType,
        AgentOption agentOption, IRequestItem? insertBefore, CancellationToken token)
    {
        IAgent agent;
        if (agentType == typeof(MiniSweAgent))
        {
            agent = new MiniSweAgent(client, agentOption);
        }
        else if (agentType == typeof(InspectAgent))
        {
            agent = new InspectAgent(client, agentOption);
        }
        else if (agentType == typeof(PlannerAgent))
        {
            agent = new PlannerAgent(client, agentOption);
        }
        else if (agentType == typeof(SummaryAgent))
        {
            agent = new SummaryAgent(client);
        }
        else if (agentType == typeof(NvidiaResearchClient))
        {
            agent = new NvidiaResearchClient(_options, client);
        }
        else
        {
            agent = (IAgent)Activator.CreateInstance(agentType)!;
        }

        var responseViewItem = new LinearResponseViewItem(this, agent)
        {
            InteractionId = requestViewItem.InteractionId
        };
        InsertResponseItem(requestViewItem, insertBefore, responseViewItem);
        return await responseViewItem.ProcessAsync(token);
    }

    public void ForkPreTask(IResponseItem dialogViewItem)
    {
        CurrentLeaf = dialogViewItem;
        MessageEventBus.Publish("您已创建新分支");
    }

    #endregion

    private readonly GlobalOptions _options;

    protected DialogSessionViewModel(GlobalOptions options, Summarizer summarizer, IDialogItem? rootNode,
        IDialogItem? currentLeaf = null)
    {
        _options = options;
        _summarizer = summarizer;
        _readOnlyDialogItems = new ReadOnlyObservableCollection<IDialogItem>(DialogItemsObservable);
        if (rootNode == null)
        {
            RootNode = new RootDialogItem();
            _currentLeaf = RootNode;
        }
        else
        {
            RootNode = rootNode;
            _currentLeaf = currentLeaf ?? RootNode;
        }

        RebuildLinearItems();
        ((INotifyCollectionChanged)this.RootNode.Children).CollectionChanged += OnRootCollectionChanged;
        SharedGraphViewModel = new DialogGraphViewModel(this);
        DialogItemsObservable.CollectionChanged += DialogOnCollectionChanged;
        OpenDialogRouteCommand =
            new ActionCommand(async void (o) =>
            {
                try
                {
                    await DialogHost.Show(new DialogGraphViewModel(this, this.RootNode.Children.FirstOrDefault()));
                }
                catch (Exception e)
                {
                    MessageBoxes.Error("无法打开对话路线图: " + e.Message);
                }
            });
        SetLeafCommand = new RelayCommand<IResponseItem>(o =>
        {
            if (o == null)
            {
                return;
            }

            ForkPreTask(o);
        });

        ToggleSearchCommand = new RelayCommand(() => { IsSearchVisible = !IsSearchVisible; });

        SearchCommand = new ActionCommand(_ =>
        {
            foreach (var dialogViewItem in this.VisualDialogItems.OfType<ISearchableDialogItem>())
            {
                dialogViewItem.SearchableDocument?.ApplySearch(_searchText);
            }

            this.FocusedHighlightItem = null;
            if (this.ScrollViewItem is ISearchableDialogItem viewItem)
            {
                viewItem.SearchableDocument?.EnsureSearch();
            }

            _highlightedText = _searchText;
        });
        GoToNextHighlightCommand = new ActionCommand(_ =>
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return;
            }

            if (_highlightedText != SearchText)
            {
                //重新搜索
                SearchCommand.Execute(null);
            }

            var searchableDialogItems = VisualDialogItems.OfType<ISearchableDialogItem>()
                .Where(item => item is { SearchableDocument.HasMatched: true })
                .ToArray();
            if (searchableDialogItems.Length == 0)
            {
                MessageEventBus.Publish("没有找到匹配的结果！");
                return;
            }

            var focusItemIndex = 0;
            CheckFocusItem(searchableDialogItems, ref focusItemIndex);
            _currentHighlightIndex++;
            if (_currentHighlightIndex >= FocusedHighlightDocument?.FoundTextRanges.Count)
            {
                //跳转到下一个FlowDocument
                focusItemIndex++;
                FocusedHighlightItem = focusItemIndex < searchableDialogItems.Length
                    ? searchableDialogItems[focusItemIndex]
                    : searchableDialogItems[0];
                _currentHighlightIndex = 0;
            }

            GoToHighlight();
        });
        GoToPreviousHighlightCommand = new ActionCommand(_ =>
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return;
            }

            if (_highlightedText != SearchText)
            {
                //重新搜索
                SearchCommand.Execute(null);
            }

            var searchableDialogItems = VisualDialogItems.OfType<ISearchableDialogItem>()
                .Where(item => item is { SearchableDocument.HasMatched: true })
                .ToArray();
            if (searchableDialogItems.Length == 0)
            {
                MessageEventBus.Publish("没有找到匹配的结果！");
                return;
            }

            var searchItemIndex = searchableDialogItems.Length - 1;
            CheckFocusItem(searchableDialogItems, ref searchItemIndex);
            _currentHighlightIndex--;
            if (_currentHighlightIndex < 0)
            {
                //跳转到上一个FlowDocument
                FocusedHighlightItem = searchItemIndex > 0
                    ? searchableDialogItems[--searchItemIndex]
                    : searchableDialogItems.Last();
                _currentHighlightIndex = FocusedHighlightDocument?.FoundTextRanges.Count - 1 ?? 0;
            }

            GoToHighlight();
        });

        ExportCommand = new ActionCommand(async void (_) =>
        {
            try
            {
                var saveFileDialog = new SaveFileDialog()
                {
                    FileName = this.Topic ?? string.Empty,
                    CheckFileExists = false,
                    AddExtension = true,
                    DefaultExt = ".md",
                    CheckPathExists = true,
                    Filter = "markdown files (*.md)|*.md"
                };
                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }

                var stringBuilder = new StringBuilder(8192);
                /*stringBuilder.AppendLine($"# {this.Topic}");
            stringBuilder.AppendLine($"### {this.DefaultClient.Name}");*/
                foreach (var viewItem in VisualDialogItems.Where(item => item.IsAvailableInContext))
                {
                    if (viewItem is ParallelResponseViewItem { AcceptedResponse: { } responseViewItem })
                    {
                        var textContent = responseViewItem.RawTextContent;
                        stringBuilder.AppendLine("# **Assistant:**");
                        stringBuilder.Append(textContent ?? string.Empty);
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine("***");
                        stringBuilder.AppendLine();
                    }
                    else if (viewItem is RequestViewItem reqViewItem)
                    {
                        stringBuilder.AppendLine("# **User:**");
                        stringBuilder.Append(reqViewItem.RawTextMessage);
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine("***");
                        stringBuilder.AppendLine();
                    }
                }

                var fileName = saveFileDialog.FileName;
                await File.WriteAllTextAsync(fileName, stringBuilder.ToString());
                MessageEventBus.Publish("已导出");
            }
            catch (Exception e)
            {
                MessageBoxes.Error(e.Message);
            }
        });
        ClearContextCommand = new ActionCommand(_ => { CutContext(); });
        ClearDialogCommand = new ActionCommand(async void (_) =>
        {
            if (await Extension.ShowConfirm("清空会话？"))
            {
                RootNode.ClearChildren();
                CurrentLeaf = RootNode;
                ScrollViewItem = null;
            }
        });
    }

    private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_options.EnableAutoSubjectGeneration &&
            e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Reset)
        {
            this.Topic = "新建会话";
        }
    }


    protected virtual void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
        PostOnPropertyChanged(nameof(Shortcut));
        OnPropertyChanged(nameof(CurrentContextTokens));
    }

    public virtual IEnumerable<IAIFunctionGroup> GetFunctionGroups()
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