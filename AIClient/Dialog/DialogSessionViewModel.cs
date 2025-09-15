﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using ClosedXML;
using Google.Apis.Util;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public abstract class DialogSessionViewModel : NotifyDataErrorInfoViewModelBase
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
                viewItem.AcceptedResponse?.SearchableDocument?.EnsureSearch();
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
                    responseViewItem.SearchableDocument?.ApplySearch(_searchText);
                }
            }
        }

        this.FocusedResponse = null;
        if (this.ScrollViewItem is MultiResponseViewItem viewItem)
        {
            viewItem.AcceptedResponse?.SearchableDocument?.EnsureSearch();
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
            .Where(item => item.AcceptedResponse is ResponseViewItem { SearchableDocument.HasMatched: true }).ToArray();
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
            .Where(item => item.AcceptedResponse is ResponseViewItem { SearchableDocument.HasMatched: true }).ToArray();
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

    public ICommand ExportCommand => new ActionCommand((async _ =>
    {
        try
        {
            var saveFileDialog = new SaveFileDialog()
            {
                AddExtension = true,
                DefaultExt = ".md", CheckPathExists = true,
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
                if (viewItem is MultiResponseViewItem multiResponseView &&
                    multiResponseView.AcceptedResponse is ResponseViewItem responseViewItem)
                {
                    var textContent = responseViewItem.TextContent;
                    stringBuilder.AppendLine("## **Assistant:**");
                    stringBuilder.Append(textContent ?? string.Empty);
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("***");
                    stringBuilder.AppendLine();
                }
                else if (viewItem is RequestViewItem reqViewItem)
                {
                    stringBuilder.AppendLine("## **User:**");
                    stringBuilder.Append(reqViewItem.TextMessage);
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

    public IDialogItem[] GenerateHistoryFromSelf(int? endIndex = null)
    {
        if (DialogItems.Count == 0)
        {
            return [];
        }

        var index = endIndex == null ? DialogItems.Count - 1 : endIndex.Value;
        var lastRequest = DialogItems[index];
        if (lastRequest is not IRequestItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        //从倒数第二条开始
        return FilterHistory(DialogItems, index - 1).Reverse().Append(lastRequest).ToArray();
    }

    public ICommand CancelLastCommand => new ActionCommand(_ =>
    {
        var multiResponseViewItem = DialogItems.LastOrDefault() as MultiResponseViewItem;
        multiResponseViewItem?.AcceptedResponse?.CancelCommand?.Execute(null);
    });

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

    public async Task<CompletedResult> InvokeOn(Func<Task<CompletedResult>> invoke)
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

    public async Task<CompletedResult> NewRequest(ILLMChatClient client, IRequestItem requestViewItem,
        int? insertIndex = null)
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
        this.IsNewResponding = true;
        CompletedResult completedResult;
        try
        {
            completedResult = await multiResponseViewItem.New(client);
        }
        finally
        {
            IsNewResponding = false;
        }

        return completedResult;
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

    public ICommand PastInteractionCommand => new ActionCommand((o => { PasteInteraction(); }));

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
                        _mapper.Map<RequestPersistItem, RequestViewItem>(requestPersistItem, (_ => { }));
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

    #endregion

    public DialogSessionViewModel(IMapper mapper, IList<IDialogItem>? dialogItems = null)
    {
        _mapper = mapper;
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
}