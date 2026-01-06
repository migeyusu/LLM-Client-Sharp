using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using ConfirmView = LLMClient.Component.UserControls.ConfirmView;

namespace LLMClient.Dialog;

public abstract class DialogSessionViewModel : NotifyDataErrorInfoViewModelBase, ITextDialogSession
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
            if (value is MultiResponseViewItem viewItem)
            {
                viewItem.AcceptedResponse?.SearchableDocument?.EnsureSearch();
                CurrentResponseViewItem = viewItem;
            }
            else
            {
                CurrentResponseViewItem = null;
            }
        }
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

    public ICommand SearchCommand { get; }

    private int _currentHighlightIndex = 0;

    private MultiResponseViewItem? _focusedResponse;

    private MultiResponseViewItem? FocusedResponse
    {
        get => _focusedResponse;
        set
        {
            _focusedResponse = value;
            var document = value?.AcceptedResponse?.SearchableDocument;
            if (document is { HasMatched: true })
            {
                document.EnsureSearch();
            }
        }
    }

    SearchableDocument? FocusedDocument
    {
        get { return FocusedResponse?.AcceptedResponse?.SearchableDocument; }
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
        var count = FocusedDocument?.FoundTextRanges.Count;
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

        var foundTextRange = FocusedDocument?.FoundTextRanges[_currentHighlightIndex];
        if (foundTextRange == null)
            return;
        var parent = FocusedDocument?.Document.Parent;
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
    public ObservableCollection<IDialogItem> DialogItems { get; }

    public ICommand ClearContextCommand { get; }

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
            if (indexOf <= 0)
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

    public ICommand ClearDialogCommand { get; }

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
        if (indexOf <= 0)
        {
            return;
        }

        for (int i = 0; i < indexOf; i++)
        {
            DialogItems.RemoveAt(0);
        }
    }

    public ICommand ClearUnavailableCommand { get; }

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
                return FilterHistory(DialogItems)
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
    public IDialogItem[] GenerateHistoryFromSelf(int? endIndex = null)
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
        return FilterHistory(DialogItems, index - 1).Reverse().Append(lastRequest).ToArray();
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
        IList<IDialogItem> history,
        MultiResponseViewItem multiResponseViewItem, string? systemPrompt = null)
    {
        CompletedResult completedResult;
        RespondingCount++;
        var responseViewItem = new ResponseViewItem(client);
        try
        {
            multiResponseViewItem.Append(responseViewItem);
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
        int? insertIndex = null, CancellationToken token = default)
    {
        var multiResponseViewItem = new MultiResponseViewItem(this)
            { InteractionId = requestViewItem.InteractionId };
        var items = this.DialogItems;
        if (insertIndex == null)
        {
            items.Add(requestViewItem);
            items.Add(multiResponseViewItem);
        }
        else
        {
            var index = insertIndex.Value;
            items.Insert(index, requestViewItem);
            index += 1;
            items.Insert(index, multiResponseViewItem);
        }

        this.ScrollViewItem = multiResponseViewItem;
        return await multiResponseViewItem.NewRequest(client, token);
    }

    #endregion

    #region copy items

    private const string CopyFormat = "LMClient.Interaction";

    /// <summary>
    /// 复制单次交互
    /// </summary>
    /// <param name="responseViewItem"></param>
    public void CopyInteraction(MultiResponseViewItem responseViewItem)
    {
        var indexOf = this.DialogItems.IndexOf(responseViewItem);
        if (indexOf < 0)
        {
            return;
        }

        var jsonObject = new JsonObject();
        var multiResponsePersistItem =
            _mapper.Map<MultiResponseViewItem, MultiResponsePersistItem>(responseViewItem, (_ => { }));
        jsonObject["response"] =
            JsonNode.Parse(JsonSerializer.Serialize(multiResponsePersistItem, Extension.DefaultJsonSerializerOptions));
        if (this.DialogItems[indexOf - 1] is RequestViewItem requestViewItem)
        {
            var requestPersistItem =
                _mapper.Map<RequestViewItem, RequestPersistItem>(requestViewItem, (_ => { }));
            jsonObject["request"] =
                JsonNode.Parse(JsonSerializer.Serialize(requestPersistItem, Extension.DefaultJsonSerializerOptions));
        }

        var data = JsonSerializer.Serialize(jsonObject, Extension.DefaultJsonSerializerOptions);
        var dataObject = new DataObject();
        dataObject.SetData(CopyFormat, data);
        Clipboard.SetDataObject(dataObject, true);
        MessageEventBus.Publish("已复制到剪贴板，可以粘贴到其他会话");
    }

    public ICommand PastInteractionCommand { get; }

    #endregion

    protected DialogSessionViewModel(IMapper mapper, IList<IDialogItem>? dialogItems = null)
    {
        _mapper = mapper;
        DialogItems = dialogItems == null
            ? []
            : new ObservableCollection<IDialogItem>(dialogItems);
        this.PredictedContextLength = GetContextTokensBefore();
        DialogItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChangedAsync(nameof(Shortcut));
            OnPropertyChanged(nameof(CurrentContextTokens));
            this.PredictedContextLength = GetContextTokensBefore();
        };
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
        SearchCommand = new ActionCommand((_ =>
        {
            foreach (var dialogViewItem in this.DialogItems)
            {
                if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
                {
                    foreach (var responseViewItem in multiResponseViewItem.Items.OfType<ResponseViewItem>())
                    {
                        responseViewItem.SearchableDocument?.ApplySearch(_searchText);
                    }
                }
                else if (dialogViewItem is RequestViewItem requestViewItem)
                {
                    requestViewItem.Document?.ApplySearch(_searchText);
                }
            }

            this.FocusedResponse = null;
            if (this.ScrollViewItem is MultiResponseViewItem viewItem)
            {
                viewItem.AcceptedResponse?.SearchableDocument?.EnsureSearch();
            }
            else if (this.ScrollViewItem is RequestViewItem requestViewItem)
            {
                requestViewItem.Document?.EnsureSearch();
            }
        }));
        GoToNextHighlightCommand = new ActionCommand((_ =>
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return;
            }

            var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
                .Where(item => item.AcceptedResponse is { SearchableDocument.HasMatched: true })
                .ToArray();
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
        GoToPreviousHighlightCommand = new ActionCommand((_ =>
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return;
            }

            var responseViewItems = DialogItems.OfType<MultiResponseViewItem>()
                .Where(item => item.AcceptedResponse is { SearchableDocument.HasMatched: true })
                .ToArray();
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
        PastInteractionCommand = new ActionCommand((_ => { PasteInteraction(); }));
        ClearContextCommand = new ActionCommand(_ => { CutContext(); });
        ClearDialogCommand = new ActionCommand(async void (_) =>
        {
            if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
            {
                DialogItems.Clear();
            }
        });
        ClearUnavailableCommand = new ActionCommand((_ =>
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
    }


    public void PasteInteraction(int insertIndex = -1)
    {
        if (Clipboard.GetDataObject() is not DataObject dataObject)
        {
            return;
        }

        if (!dataObject.GetDataPresent(CopyFormat))
        {
            return;
        }

        if (dataObject.GetData(CopyFormat) is not string data)
        {
            return;
        }

        try
        {
            var jsonNode = JsonNode.Parse(data);
            if (jsonNode is not JsonObject jsonObject)
            {
                return;
            }

            if (jsonObject.TryGetPropertyValue("request", out var requestNode) && requestNode != null)
            {
                var requestPersistItem =
                    requestNode.Deserialize<RequestPersistItem>(Extension.DefaultJsonSerializerOptions);
                if (requestPersistItem != null)
                {
                    var requestViewItem =
                        _mapper.Map<RequestPersistItem, RequestViewItem>(requestPersistItem,
                            new RequestViewItem(requestPersistItem.RawTextMessage ?? string.Empty, this),
                            (_ => { }));
                    if (this.DialogItems.Contains(requestViewItem, DialogItemEqualityComparer.Instance))
                    {
                        return;
                    }

                    if (insertIndex == -1)
                    {
                        this.DialogItems.Add(requestViewItem);
                    }
                    else
                    {
                        this.DialogItems.Insert(insertIndex, requestViewItem);
                        insertIndex++;
                    }
                }
            }

            if (jsonObject.TryGetPropertyValue("response", out var responseNode) && responseNode != null)
            {
                var multiResponsePersistItem =
                    responseNode.Deserialize<MultiResponsePersistItem>(Extension.DefaultJsonSerializerOptions);
                if (multiResponsePersistItem != null)
                {
                    var multiResponseViewItem =
                        _mapper.Map<MultiResponsePersistItem, MultiResponseViewItem>(multiResponsePersistItem,
                            options =>
                            {
                                options.Items.Add(AutoMapModelTypeConverter.ParentSessionViewModelKey, this);
                            });
                    if (this.DialogItems.Contains(multiResponseViewItem, DialogItemEqualityComparer.Instance))
                    {
                        return;
                    }

                    // multiResponseViewItem.ParentSession = this;
                    if (insertIndex == -1)
                    {
                        this.DialogItems.Add(multiResponseViewItem);
                    }
                    else
                    {
                        this.DialogItems.Insert(insertIndex, multiResponseViewItem);
                    }
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "粘贴失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected virtual void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.IsDataChanged = true;
    }
}