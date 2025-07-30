using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public abstract class DialogSessionViewModel : NotifyDataErrorInfoViewModelBase
{
    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public virtual bool IsDataChanged { get; set; } = true;

    public event Action<CompletedResult>? ResponseCompleted;

    public string? Shortcut
    {
        get
        {
            return DialogItems.OfType<MultiResponseViewItem>()
                .FirstOrDefault(item => item.IsAvailableInContext)
                ?.CurrentResponse?.TextWithoutThinking;
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

    #region scroll

    private IDialogItem? _scrollViewItem;

    public IDialogItem? ScrollViewItem
    {
        get => _scrollViewItem;
        set
        {
            if (Equals(value, _scrollViewItem)) return;
            _scrollViewItem = value;
            OnPropertyChanged();
            if (value is MultiResponseViewItem viewItem)
            {
                viewItem.CurrentResponse?.Document?.EnsureSearch();
            }
        }
    }

    public ICommand ScrollToPreviousCommand => new ActionCommand(_ =>
    {
        if (DialogItems.Count == 0)
        {
            return;
        }

        var scrollViewItem = this.ScrollViewItem;
        if (scrollViewItem != null)
        {
            var indexOf = this.DialogItems.IndexOf(scrollViewItem);
            if (indexOf > 0)
            {
                this.ScrollViewItem = this.DialogItems[indexOf - 1];
            }
        }
        else
        {
            this.ScrollViewItem = this.DialogItems.FirstOrDefault();
        }
    });

    public ICommand ScrollToNextCommand => new ActionCommand(o =>
    {
        if (DialogItems.Count == 0)
        {
            return;
        }

        var scrollViewItem = this.ScrollViewItem;
        if (scrollViewItem != null)
        {
            var indexOf = this.DialogItems.IndexOf(scrollViewItem);
            if (indexOf == -1)
            {
                return;
            }

            if (indexOf == this.DialogItems.Count - 1)
            {
                MessageEventBus.Publish("已经是最后一条了！");
                return;
            }

            this.ScrollViewItem = this.DialogItems[indexOf + 1];
        }
        else
        {
            this.ScrollViewItem = this.DialogItems.LastOrDefault();
        }
    });

    public ICommand ScrollToLastItemCommand => new ActionCommand((o => { ScrollToLast(); }));

    public void ScrollToLast()
    {
        if (!DialogItems.Any())
        {
            return;
        }

        var scrollViewItem = DialogItems.Last();
        if (scrollViewItem == this.ScrollViewItem)
        {
            MessageEventBus.Publish("已经是最后一条了！");
            return;
        }

        this.ScrollViewItem = scrollViewItem;
    }

    public ICommand ScrollToFirstItemCommand => new ActionCommand((o => { ScrollToFirst(); }));

    public void ScrollToFirst()
    {
        if (!DialogItems.Any())
        {
            return;
        }

        var scrollViewItem = DialogItems.First();
        if (scrollViewItem == this.ScrollViewItem)
        {
            MessageEventBus.Publish("已经是第一条了！");
            return;
        }

        this.ScrollViewItem = scrollViewItem;
    }

    #endregion

    #region search

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

    public ICommand SearchCommand => new ActionCommand((o =>
    {
        foreach (var dialogViewItem in this.DialogItems)
        {
            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                foreach (var responseViewItem in multiResponseViewItem.Items.OfType<ResponseViewItem>())
                {
                    responseViewItem.Document?.ApplySearch(_searchText);
                }
            }
        }

        this.FocusedResponse = null;
        if (this.ScrollViewItem is MultiResponseViewItem viewItem)
        {
            viewItem.CurrentResponse?.Document?.EnsureSearch();
        }
    }));

    private int _currentHighlightIndex = 0;

    private MultiResponseViewItem? _focusedResponse;

    private MultiResponseViewItem? FocusedResponse
    {
        get => _focusedResponse;
        set
        {
            _focusedResponse = value;
            var document = value?.CurrentResponse?.Document;
            if (document is { HasMatched: true })
            {
                document.EnsureSearch();
            }
        }
    }

    SearchableDocument? FocusedDocument
    {
        get { return FocusedResponse?.CurrentResponse?.Document; }
    }

    private void CheckFocusResponse(IList<MultiResponseViewItem> responseViewItems, ref int responseIndex)
    {
        if (FocusedResponse != null)
        {
            responseIndex = responseViewItems.IndexOf(FocusedResponse);
            if (responseIndex == -1)
            {
                FocusedResponse = null;
                responseIndex = 0;
            }
        }

        if (FocusedResponse == null)
        {
            this.FocusedResponse = responseViewItems.First();
        }
    }

    private void GoToHighlight()
    {
        ScrollViewItem = this.FocusedResponse;
        var foundTextRange = FocusedDocument?.FoundTextRanges[_currentHighlightIndex];
        if (foundTextRange == null)
            return;
        var parent = FocusedDocument?.Document.Parent;
        if (parent is FlowDocumentScrollViewerEx ex)
        {
            ex.ScrollToRange(foundTextRange);
        }
    }

    public ICommand GoToNextHighlightCommand => new ActionCommand((o =>
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
            .Where(item => item.AcceptedResponse is ResponseViewItem { Document.HasMatched: true }).ToArray();
        if (responseViewItems.Length == 0)
        {
            MessageEventBus.Publish("没有找到匹配的结果！");
            return;
        }

        var responseIndex = 0;
        CheckFocusResponse(responseViewItems, ref responseIndex);
        _currentHighlightIndex++;
        if (_currentHighlightIndex >= FocusedDocument?.FoundTextRanges.Count)
        {
            //跳转到下一个FlowDocument
            responseIndex++;
            FocusedResponse = responseIndex < responseViewItems.Length
                ? responseViewItems[responseIndex]
                : responseViewItems[0];
            _currentHighlightIndex = 0;
        }

        GoToHighlight();
    }));

    public ICommand GoToPreviousHighlightCommand => new ActionCommand((o =>
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
            .Where(item => item.AcceptedResponse is ResponseViewItem { Document.HasMatched: true }).ToArray();
        if (responseViewItems.Length == 0)
        {
            MessageEventBus.Publish("没有找到匹配的结果！");
            return;
        }

        var responseIndex = responseViewItems.Length - 1;
        CheckFocusResponse(responseViewItems, ref responseIndex);
        _currentHighlightIndex--;
        if (_currentHighlightIndex < 0)
        {
            //跳转到上一个FlowDocument
            FocusedResponse = responseIndex > 0
                ? responseViewItems[--responseIndex]
                : responseViewItems.Last();
            _currentHighlightIndex = FocusedDocument?.FoundTextRanges.Count - 1 ?? 0;
        }

        GoToHighlight();
    }));

    #endregion

    #region items management

    /// <summary>
    /// dialog level prompt
    /// </summary>
    public abstract string? SystemPrompt { get; set; }

    public ObservableCollection<IDialogItem> DialogItems { get; }

    public ICommand ClearContextCommand => new ActionCommand(o => { CutContext(); });

    public void CutContext(IRequestItem? requestViewItem = null)
    {
        if (DialogItems.Count == 0)
        {
            return;
        }

        var item = new EraseViewItem();
        if (requestViewItem != null)
        {
            var indexOf = DialogItems.IndexOf(requestViewItem);
            if (indexOf < 0)
            {
                return;
            }

            DialogItems.Insert(indexOf, item);
        }
        else
        {
            DialogItems.Add(item);
        }

        ScrollViewItem = item;
    }

    public ICommand ClearDialogCommand => new ActionCommand(async o =>
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            DialogItems.Clear();
        }
    });

    public virtual void DeleteItem(IDialogItem item)
    {
        var indexOf = this.DialogItems.IndexOf(item);
        if (item is IRequestItem requestViewItem)
        {
            var interactionId = requestViewItem.InteractionId;
            // 删除所有与该请求相关的响应
            if (indexOf < this.DialogItems.Count - 1)
            {
                var index = indexOf + 1;
                var dialogViewItem = this.DialogItems[index];
                if (dialogViewItem is MultiResponseViewItem multiResponseViewItem &&
                    multiResponseViewItem.InteractionId == interactionId)
                {
                    this.DialogItems.RemoveAt(index);
                }
            }
        }

        this.DialogItems.RemoveAt(indexOf);
        if (ScrollViewItem == item)
        {
            ScrollViewItem = this.DialogItems.LastOrDefault();
        }
    }

    public async Task AppendResponseOn(MultiResponseViewItem responseViewItem, ILLMClient client)
    {
        var dialogItems = GenerateHistory(responseViewItem);
        await SendRequestCore(client, dialogItems, responseViewItem, this.SystemPrompt);
    }

    public void RemoveAfter(MultiResponseViewItem responseViewItem)
    {
        //删除之后的所有记录
        var indexOf = DialogItems.IndexOf(responseViewItem);
        var dialogCount = DialogItems.Count;
        for (int i = 0; i < dialogCount - indexOf; i++)
        {
            DialogItems.RemoveAt(indexOf);
        }
    }

    public async void ClearBefore(RequestViewItem requestViewItem)
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            RemoveBefore(requestViewItem);
        }
    }

    public void RemoveBefore(RequestViewItem request)
    {
        //删除之前的所有记录
        var indexOf = DialogItems.IndexOf(request);
        if (indexOf < 0)
        {
            return;
        }

        for (int i = 0; i < indexOf; i++)
        {
            DialogItems.RemoveAt(0);
        }
    }

    public ICommand ClearUnavailableCommand => new ActionCommand((o =>
    {
        var deleteItems = new List<IDialogItem>();
        var unusedInteractionId = Guid.Empty;
        for (var index = DialogItems.Count - 1; index >= 0; index--)
        {
            var dialogViewItem = DialogItems[index];
            if (dialogViewItem is MultiResponseViewItem item && !item.HasAvailableMessage)
            {
                deleteItems.Add(dialogViewItem);
                unusedInteractionId = item.InteractionId;
            }
            else if (dialogViewItem is RequestViewItem requestViewItem &&
                     requestViewItem.InteractionId == unusedInteractionId)
            {
                deleteItems.Add(requestViewItem);
            }
        }

        deleteItems.ForEach(item => DialogItems.Remove(item));
    }));

    #endregion

    public long CurrentContextTokens
    {
        get
        {
            if (!DialogItems.Any())
            {
                return 0;
            }

            return FilterHistory(DialogItems)
                .Sum(item => item.Tokens);
        }
    }

    #region core methods

    private static IEnumerable<IDialogItem> FilterHistory(IList<IDialogItem> source, int? endIndex = null)
    {
        endIndex ??= source.Count - 1;
        Guid? interactionId = null;
        for (int i = endIndex.Value; i >= 0; i--)
        {
            var dialogViewItem = source[i];
            if (dialogViewItem is EraseViewItem)
            {
                yield break;
            }

            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                if (multiResponseViewItem.IsAvailableInContext)
                {
                    interactionId = multiResponseViewItem.InteractionId;
                    yield return multiResponseViewItem;
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
                    yield return requestViewItem;
                }
            }
        }
    }

    private static IDialogItem[] GenerateHistory(IList<IDialogItem> total)
    {
        var lastRequest = total[^1];
        if (lastRequest is not IRequestItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        //从倒数第二条开始
        return FilterHistory(total, total.Count - 2).Reverse().Append(lastRequest).ToArray();
    }

    public IDialogItem[] GenerateHistory(MultiResponseViewItem? response = null)
    {
        IDialogItem[] dialogViewItems;
        if (response != null)
        {
            //获得之前的所有请求
            var indexOf = DialogItems.IndexOf(response);
            if (indexOf < 1)
            {
                return [];
            }

            dialogViewItems = DialogItems.Take(indexOf).ToArray();
        }
        else
        {
            dialogViewItems = DialogItems.ToArray(); //copy
        }

        return GenerateHistory(dialogViewItems);
    }

    public ICommand CancelLastCommand => new ActionCommand(_ =>
    {
        var multiResponseViewItem = DialogItems.LastOrDefault() as MultiResponseViewItem;
        if (multiResponseViewItem?.AcceptedResponse is RespondingViewItem respondingViewItem)
        {
            respondingViewItem.CancelCommand.Execute(null);
        }
    });

    private static IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

    public async Task<CompletedResult> SendRequestCore(ILLMClient client,
        IList<IDialogItem> history,
        MultiResponseViewItem multiResponseViewItem, string? systemPrompt = null)
    {
        var completedResult = CompletedResult.Empty;
        if (client.IsResponding)
        {
            MessageEventBus.Publish("模型正在响应中，请稍后再试。");
            return completedResult;
        }

        RespondingCount++;
        var respondingViewItem = new RespondingViewItem(client);
        try
        {
            multiResponseViewItem.Append(respondingViewItem);
            completedResult = await client.SendRequest(new DialogContext(history, systemPrompt),
                cancellationToken: respondingViewItem.RequestTokenSource.Token);
            var responseViewItem = new ResponseViewItem(client);
            Mapper.Map<IResponse, ResponseViewItem>(completedResult, responseViewItem);
            multiResponseViewItem.Append(responseViewItem);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "请求异常", MessageBoxButton.OK, MessageBoxImage.Error);
            completedResult.ErrorMessage = exception.Message;
        }
        finally
        {
            respondingViewItem.RequestTokenSource.Dispose();
            multiResponseViewItem.Remove(respondingViewItem);
            RespondingCount--;
        }

        this.TokensConsumption += completedResult.Usage?.TotalTokenCount ?? 0;
        this.TotalPrice += completedResult.Price ?? 0;
        OnResponseCompleted(completedResult);
        return completedResult;
    }

    public async Task<CompletedResult> NewRequest(ILLMClient client, IRequestItem requestViewItem)
    {
        var items = this.DialogItems;
        items.Add(requestViewItem);
        this.ScrollViewItem = requestViewItem;
        var history = this.GenerateHistory();
        var multiResponseViewItem = new MultiResponseViewItem(this)
            { InteractionId = requestViewItem.InteractionId };
        items.Add(multiResponseViewItem);
        this.IsNewResponding = true;
        try
        {
            var result = await SendRequestCore(client, history, multiResponseViewItem, this.SystemPrompt);
            return result;
        }
        finally
        {
            IsNewResponding = false;
        }
    }

    #endregion

    public DialogSessionViewModel(IList<IDialogItem>? dialogItems = null)
    {
        DialogItems = dialogItems == null
            ? []
            : new ObservableCollection<IDialogItem>(dialogItems);
        DialogItems.CollectionChanged += (sender, args) =>
        {
            OnPropertyChangedAsync(nameof(Shortcut));
            OnPropertyChanged(nameof(CurrentContextTokens));
        };
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
    }

    private void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }

    protected virtual void OnResponseCompleted(CompletedResult obj)
    {
        ResponseCompleted?.Invoke(obj);
    }
}