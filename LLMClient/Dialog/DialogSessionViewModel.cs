using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
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

    private MultiResponseViewItem? _currentResponseViewItem;

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
        return item is MultiResponseViewItem;
    }

    public IReadOnlyList<IDialogItem> DialogItems
    {
        get { return _readOnlyDialogItems; }
    }

    public ICommand ClearContextCommand { get; }

    public ICommand OpenDialogRouteCommand { get; }

    /// <summary>
    /// 用于子项路由选择
    /// </summary>
    public DialogGraphViewModel SharedGraphViewModel { get; }

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
            CurrentLeaf = previousItem ?? RootNode;
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
                if (CurrentLeaf is MultiResponseViewItem responseViewItem)
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

    public virtual string? Name { get; set; }

    public ICommand ExportCommand { get; }

    #region core methods

    public virtual async Task<CompletedResult> InvokeRequest(ResponseViewItem responseViewItem,
        MultiResponseViewItem multiResponseViewItem)
    {
        RespondingCount++;
        try
        {
            var dialogItems = multiResponseViewItem.GetChatHistory().ToArray();
            var dialogContext = new DialogContext(dialogItems, this.SystemPrompt);
            var completedResult = await responseViewItem.ProcessRequest(dialogContext);
            this.TokensConsumption += completedResult.Usage?.TotalTokenCount ?? 0;
            this.TotalPrice += completedResult.Price ?? 0;
            return completedResult;
        }
        finally
        {
            RespondingCount--;
        }
    }

    public async Task<CompletedResult> NewResponse(ILLMChatClient client, IRequestItem requestViewItem,
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
        return await multiResponseViewItem.AppendResponse(client, token);
    }

    public void ForkPreTask(MultiResponseViewItem dialogViewItem)
    {
        CurrentLeaf = dialogViewItem;
        MessageEventBus.Publish("您已创建新分支");
    }

    #endregion

    protected DialogSessionViewModel(IDialogItem? rootNode, IDialogItem? currentLeaf = null)
    {
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
        SharedGraphViewModel = new DialogGraphViewModel(this);
        DialogItemsObservable.CollectionChanged += (_, _) =>
        {
            OnPropertyChangedAsync(nameof(Shortcut));
            OnPropertyChanged(nameof(CurrentContextTokens));
        };
        this.DialogItemsObservable.CollectionChanged += DialogOnCollectionChanged;
        OpenDialogRouteCommand =
            new ActionCommand(async o =>
            {
                await DialogHost.Show(new DialogGraphViewModel(this, this.RootNode.Children.FirstOrDefault()));
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
        });

        ExportCommand = new ActionCommand(async void (_) =>
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
                foreach (var viewItem in DialogItems.Where(item => item.IsAvailableInContext))
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
        });
        ClearContextCommand = new ActionCommand(_ => { CutContext(); });
        ClearDialogCommand = new ActionCommand(async void (_) =>
        {
            if (await DialogHost.Show(new ConfirmView() { Header = "清空会话？" }) is true)
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