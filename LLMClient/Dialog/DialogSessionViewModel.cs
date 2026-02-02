using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Controls;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using ConfirmView = LLMClient.Component.UserControls.ConfirmView;

namespace LLMClient.Dialog;

public abstract class DialogSessionViewModel : NotifyDataErrorInfoViewModelBase, ITextDialogSession,
    INavigationViewModel
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public virtual bool IsDataChanged { get; set; } = true;

    public string? Shortcut
    {
        get
        {
            var textContent = DialogItems.OfType<MultiResponseViewItem>()
                .FirstOrDefault(item => item.IsAvailableInContext)
                ?.AcceptedResponse?.TextContent;
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

    private int _respondingCount;

    public int RespondingCount
    {
        get => _respondingCount;
        protected set
        {
            if (value == _respondingCount) return;
            _respondingCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    private long _tokensConsumption;

    public long TokensConsumption
    {
        get => _tokensConsumption;
        set
        {
            if (value == _tokensConsumption) return;
            _tokensConsumption = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 预测的上下文长度
    /// </summary>
    public long PredictedContextLength
    {
        get => _predictedContextLength;
        set
        {
            if (value == _predictedContextLength) return;
            _predictedContextLength = value;
            OnPropertyChanged();
        }
    }

    private double _totalPrice;

    public double TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    private IList<CheckableFunctionGroupTree>? _selectedFunctionGroups;

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


    #region scroll

    public MultiResponseViewItem? CurrentResponseViewItem
    {
        get => _currentResponseViewItem;
        set
        {
            if (Equals(value, _currentResponseViewItem)) return;
            _currentResponseViewItem = value;
            OnPropertyChanged();
        }
    }

    private IDialogItem? _scrollViewItem;

    public IDialogItem? ScrollViewItem
    {
        get => _scrollViewItem;
        set
        {
            if (Equals(value, _scrollViewItem)) return;
            _scrollViewItem = value;
            OnPropertyChanged();
            if (value is ISearchableDialogItem viewItem)
            {
                viewItem.SearchableDocument?.EnsureSearch();
            }

            CurrentResponseViewItem = value as MultiResponseViewItem;
        }
    }

    #endregion

    #region search

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

    private ISearchableDialogItem? _focusedResponse;

    private ISearchableDialogItem? FocusedHighlightItem
    {
        get => _focusedResponse;
        set
        {
            _focusedResponse = value;
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

    #endregion

    #region items management

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
            RebuildLinearItems();
        }
    }

    private void RebuildLinearItems()
    {
        var newList = CurrentLeaf.PathFromRoot();
        ObservableCollectionPatcher.PatchByLcs(DialogItemsObservable, newList,
            (a) => a.Id);
        // DialogItemsObservable.ResetWith(newList);
    }

    public SuspendableObservableCollection<IDialogItem> DialogItemsObservable { get; } = [];

    private readonly ReadOnlyObservableCollection<IDialogItem> _readOnlyDialogItems;

    public bool IsNodeSelectable(IDialogItem item)
    {
        return item is MultiResponseViewItem;
    }

    public IReadOnlyList<IDialogItem> DialogItems
    {
        get { return _readOnlyDialogItems; }
    }

    public ICommand ClearContextCommand { get; }

    public ICommand OpenDialogRouteCommand { get; }

//todo:测试动态插入时的UI性能

    /// <summary>
    /// 通过插入擦除标记来切断上下文
    /// </summary>
    /// <param name="requestViewItem">if null:last</param>
    public void CutContext(IRequestItem? requestViewItem = null)
    {
        if (DialogItems.Count == 0)
        {
            return;
        }

        var eraseViewItem = new EraseViewItem();
        if (requestViewItem != null)
        {
            var indexOf = DialogItems.IndexOf(requestViewItem);
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

    public ICommand ClearDialogCommand { get; }

    public virtual void DeleteItem(IDialogItem item)
    {
        var previousItem = item.PreviousItem;
        if (item is IRequestItem requestItem)
        {
            requestItem.DeleteInteraction();
        }
        else if (item is EraseViewItem eraseViewItem)
        {
            eraseViewItem.Delete();
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
    }

    public async void RemoveAfter(MultiResponseViewItem responseViewItem)
    {
        if (responseViewItem.Children.Count == 0)
        {
            return;
        }

        if (responseViewItem.Children.Count > 1)
        {
            if ((await DialogHost.Show(new ConfirmView() { Header = "该节点后包含分支，依然清空？" })) is not true)
            {
                return;
            }
        }

        responseViewItem.ClearChildren();
        CurrentLeaf = responseViewItem;
    }

    #endregion

    public long CurrentContextTokens
    {
        get
        {
            if (!DialogItems.Any())
            {
                return 0;
            }

            try
            {
                return GetChatHistory(CurrentLeaf)
                    .Sum(item => item.Tokens);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public virtual string? Name { get; set; }

    public ICommand ExportCommand { get; }

    #region core methods

    private static IEnumerable<IDialogItem> GetChatHistory(IDialogItem lastItem)
    {
        if (lastItem is not MultiResponseViewItem)
        {
            yield break;
        }

        Guid? interactionId = null;
        for (var dialogViewItem = lastItem; dialogViewItem != null; dialogViewItem = dialogViewItem.PreviousItem)
        {
            if (dialogViewItem is EraseViewItem)
            {
                yield break;
            }

            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                if (multiResponseViewItem.IsResponding)
                {
                    throw new InvalidOperationException("无法生成包含正在响应的记录的历史");
                }

                if (multiResponseViewItem.IsAvailableInContext)
                {
                    interactionId = multiResponseViewItem.InteractionId;
                    yield return multiResponseViewItem;
                }
                else
                {
                    //这样的设计允许中间有不可用的响应（跳过）
                    interactionId = null;
                }
            }
            else if (dialogViewItem is RequestViewItem requestViewItem)
            {
                if (interactionId == requestViewItem.InteractionId)
                {
                    yield return requestViewItem;
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="endIndex">if null: last index</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public IReadOnlyList<IDialogItem> GenerateHistoryFromSelf(int? endIndex = null)
    {
        if (DialogItems.Count == 0)
        {
            return [];
        }

        var index = endIndex ?? DialogItems.Count - 1;
        var lastRequest = DialogItems[index];
        if (lastRequest is not IRequestItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        //从倒数第二条开始
        return GetChatHistory(DialogItems[index - 1]).Reverse().Append(lastRequest).ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="responseViewItem">if null, from last</param>
    /// <returns></returns>
    public long GetContextTokensBefore(MultiResponseViewItem? responseViewItem = null)
    {
        long contextLength = 0;
        var source = this.DialogItems;
        var endIndex = responseViewItem == null
            ? source.Count - 1
            : source.IndexOf(responseViewItem);
        if (endIndex < 0)
        {
            return contextLength;
        }

        Guid? interactionId = null;
        for (int i = endIndex; i >= 0; i--)
        {
            var dialogViewItem = source[i];
            if (dialogViewItem is EraseViewItem)
            {
                break;
            }

            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                if (multiResponseViewItem.IsResponding)
                {
                    break;
                }

                if (multiResponseViewItem.IsAvailableInContext)
                {
                    interactionId = multiResponseViewItem.InteractionId;
                    contextLength += multiResponseViewItem.Tokens;
                }
                else
                {
                    interactionId = null;
                }
            }
            else if (dialogViewItem is RequestViewItem requestViewItem)
            {
                if (interactionId == requestViewItem.InteractionId)
                {
                    contextLength += requestViewItem.Tokens;
                }
            }
        }

        return contextLength;
    }


    public DialogContext CreateDialogContextBefore(MultiResponseViewItem? responseViewItem = null)
    {
        int? endIndex = null;
        if (responseViewItem != null)
        {
            //获得之前的所有请求
            var indexOf = DialogItems.IndexOf(responseViewItem);
            if (indexOf >= 1)
            {
                endIndex = indexOf - 1;
            }
        }


        var dialogItems = GenerateHistoryFromSelf(endIndex);
        return new DialogContext(dialogItems, this.SystemPrompt);
    }

    /// <summary>
    /// 核心请求方法，负责统计请求数据以及插入上下文操作
    /// </summary>
    /// <param name="invoke"></param>
    /// <returns></returns>
    public virtual async Task<CompletedResult> InvokeRequest(Func<Task<CompletedResult>> invoke)
    {
        RespondingCount++;
        CompletedResult completedResult;
        try
        {
            completedResult = await invoke.Invoke();
        }
        finally
        {
            RespondingCount--;
        }

        this.TokensConsumption += completedResult.Usage?.TotalTokenCount ?? 0;
        this.TotalPrice += completedResult.Price ?? 0;
        return completedResult;
    }

    private readonly IMapper _mapper;
    private long _predictedContextLength;
    private MultiResponseViewItem? _currentResponseViewItem;

    public async Task<CompletedResult> AddNewResponse(ILLMChatClient client,
        IReadOnlyList<IDialogItem> history,
        MultiResponseViewItem multiResponseViewItem, string? systemPrompt = null)
    {
        CompletedResult completedResult;
        RespondingCount++;
        var responseViewItem = new ResponseViewItem(client);
        try
        {
            multiResponseViewItem.AppendResponse(responseViewItem);
            var dialogContext = new DialogContext(history, systemPrompt);
            completedResult = await responseViewItem.SendRequest(dialogContext);
        }
        finally
        {
            RespondingCount--;
        }

        this.TokensConsumption += completedResult.Usage?.TotalTokenCount ?? 0;
        this.TotalPrice += completedResult.Price ?? 0;
        return completedResult;
    }

    public virtual async Task<CompletedResult> NewRequest(ILLMChatClient client, IRequestItem requestViewItem,
        IRequestItem? insertBefore = null, CancellationToken token = default)
    {
        var multiResponseViewItem = new MultiResponseViewItem(this)
            { InteractionId = requestViewItem.InteractionId };
        if (insertBefore == null)
        {
            this.CurrentLeaf.AppendChild(requestViewItem)
                .AppendChild(multiResponseViewItem);
            this.CurrentLeaf = multiResponseViewItem;
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
                .AppendChild(multiResponseViewItem)
                .AppendChild(insertBefore);
            RebuildLinearItems();
        }

        this.ScrollViewItem = multiResponseViewItem;
        return await multiResponseViewItem.NewRequest(client, token);
    }

    public void ForkPreTask(MultiResponseViewItem dialogViewItem)
    {
        CurrentLeaf = dialogViewItem;
    }

    #endregion

    protected DialogSessionViewModel(IMapper mapper, IDialogItem? rootNode, IDialogItem? currentLeaf = null)
    {
        _mapper = mapper;
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
        this.PredictedContextLength = GetContextTokensBefore();
        DialogItemsObservable.CollectionChanged += (_, _) =>
        {
            OnPropertyChangedAsync(nameof(Shortcut));
            OnPropertyChanged(nameof(CurrentContextTokens));
            this.PredictedContextLength = GetContextTokensBefore();
        };
        this.DialogItemsObservable.CollectionChanged += DialogOnCollectionChanged;
        OpenDialogRouteCommand = new ActionCommand(async o =>
        {
            await DialogHost.Show(new DialogGraphViewModel(this));
        });
        SearchCommand = new ActionCommand(_ =>
        {
            foreach (var dialogViewItem in this.DialogItems.OfType<ISearchableDialogItem>())
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
        GoToNextHighlightCommand = new ActionCommand((_ =>
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

            var searchableDialogItems = DialogItems.OfType<ISearchableDialogItem>()
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
        }));
        GoToPreviousHighlightCommand = new ActionCommand((_ =>
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

            var searchableDialogItems = DialogItems.OfType<ISearchableDialogItem>()
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
        }));

        ExportCommand = new ActionCommand((async void (_) =>
        {
            try
            {
                var saveFileDialog = new SaveFileDialog()
                {
                    FileName = this.Name ?? string.Empty,
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
                foreach (var viewItem in DialogItems.Where((item => item.IsAvailableInContext)))
                {
                    if (viewItem is MultiResponseViewItem { AcceptedResponse: { } responseViewItem })
                    {
                        var textContent = responseViewItem.TextContent;
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
                MessageBox.Show(e.Message);
            }
        }));
        ClearContextCommand = new ActionCommand(_ => { CutContext(); });
        ClearDialogCommand = new ActionCommand(async void (_) =>
        {
            if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
            {
                RootNode.ClearChildren();
                CurrentLeaf = RootNode;
                ScrollViewItem = null;
            }
        });
    }

    protected virtual void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }
}