// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using LLMClient.UI.MCP.Servers;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public class DialogViewModel : BaseViewModel
{
    /// <summary>
    /// 正在处理
    /// </summary>
    public bool IsBusy
    {
        get => RespondingCount > 0;
    }

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = false;

    public int RespondingCount
    {
        get => _respondingCount;
        private set
        {
            if (value == _respondingCount) return;
            _respondingCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public string Topic
    {
        get => _topic;
        set
        {
            if (value == _topic) return;
            _topic = value;
            OnPropertyChanged();
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

    public ObservableCollection<IDialogItem> DialogItems { get; }

    /// <summary>
    /// dialog level prompt
    /// </summary>
    public string? DialogPrompt
    {
        get => _dialogPrompt;
        set
        {
            if (value == _dialogPrompt) return;
            _dialogPrompt = value;
            OnPropertyChanged();
        }
    }

    public string? Shortcut
    {
        get
        {
            return DialogItems.OfType<MultiResponseViewItem>()
                .FirstOrDefault(item => { return item.IsAvailableInContext; })
                ?.CurrentResponse?.TextWithoutThinking;
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
        var client = this.DefaultClient;
        this.RespondingCount++;
        this.IsChaining = true;
        this.ChainingStep = 0;
        var pendingItems = dialogItems
            .Where(item => item is RequestViewItem || item is EraseViewItem)
            .ToArray();
        this.ChainStepCount = pendingItems.Length;
        try
        {
            foreach (var oldDialogDialogItem in pendingItems)
            {
                if (oldDialogDialogItem is RequestViewItem requestViewItem)
                {
                    var newGuid = Guid.NewGuid();
                    var newItem = requestViewItem.Clone();
                    DialogItems.Add(newItem);
                    var copy = DialogItems.ToArray();
                    int retryCount = 3;
                    while (retryCount > 0)
                    {
                        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = newGuid };
                        DialogItems.Add(multiResponseViewItem);
                        var completedResult =
                            await SendRequestCore(client, copy, multiResponseViewItem, this.DialogPrompt);
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
                    this.DialogItems.Add(new EraseViewItem());
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
            this.RespondingCount--;
            this.IsChaining = false;
            /*this.ChainStepCount = 0;
            this.ChainingStep = 0;*/
            ScrollViewItem = DialogItems.Last();
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

    #region function call

    public bool MCPEnabled
    {
        get => _mcpEnabled;
        set
        {
            if (value == _mcpEnabled) return;
            _mcpEnabled = value;
            OnPropertyChanged();
        }
    }

    public IList<IAIFunctionGroup>? SelectedFunctions { get; set; }

    #endregion

    #region toolbar

    public ICommand ClearContextCommand => new ActionCommand((o =>
    {
        var item = new EraseViewItem();
        DialogItems.Add(item);
        ScrollViewItem = item;
    }));

    public ICommand ClearDialogCommand => new ActionCommand(async o =>
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            DialogItems.Clear();
        }
    });

    public ICommand ClearUnavailableCommand => new ActionCommand((o =>
    {
        var deleteItems = new List<IDialogItem>();
        Guid unusedInteractionId = Guid.Empty;
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

    public ICommand ChangeModelCommand => new ActionCommand((async o =>
    {
        var selectionViewModel = new DialogCreationViewModel(EndpointService);
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
    }));

    public ICommand ExportCommand => new ActionCommand((async o =>
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
        stringBuilder.AppendLine($"# {this.Topic}");
        stringBuilder.AppendLine($"### {this.DefaultClient.Name}");
        foreach (var viewItem in this.DialogItems.Where((item => item.IsAvailableInContext)))
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
    }));

    #endregion

    /*public async Task AddFileToContext(FileInfo fileInfo)
    {
        var build = Kernel.CreateBuilder().Build();
        DocumentPlugin documentPlugin = new(new WordDocumentConnector(), new LocalFileSystemConnector());
        var doc = await documentPlugin.ReadTextAsync(fileInfo.FullName);
        // 2. 简单按 500 tokens 左右切块（示例用行数）
        var memory = build.GetRequiredService<ISemanticTextMemory>();
        var chunks = SplitByLength(doc, maxChars: 1500);

        IEnumerable<string> SplitByLength(string text, int maxChars)
        {
            var span = text.AsMemory();
            int offset = 0;
            while (offset < span.Length)
            {
                int length = Math.Min(maxChars, span.Length - offset);
                var chunk = span.Slice(offset, length).ToString();
                offset += length;
                yield return chunk;
            }
        }

        Guid docId = Guid.NewGuid();
        // 3. 存向量
        int i = 0;
        foreach (var chunk in chunks)
        {
            await memory.SaveInformationAsync(
                collection: "docs", // 向量库里的“表”或“namespace”
                text: chunk,
                id: $"{docId}_{i++}", // 唯一键
                description: $"doc:{docId}" // 可做元数据
            );
        }

        // var embeddingGenerator = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        // var userQuestion = await embeddingGenerator.GenerateAsync("DDC的初始化有几个步骤？");
        var searchResults = memory.SearchAsync(
            collection: "docs",
            query: "DDC的初始化有几个步骤？",
            limit: 6, // 取前 6 段
            minRelevanceScore: 0.7 // 可调
        );
        Debugger.Break();
    }*/

    /*public ICommand TestCommand => new ActionCommand((async o =>
    {
        if (this.Client == null)
        {
            return;
        }

        var dialogViewItem = this.DialogItems.Last(item => item is MultiResponseViewItem && item.IsAvailableInContext);
        var multiResponseViewItem = dialogViewItem as MultiResponseViewItem;
        var endpoint = EndpointService.AvailableEndpoints[0];
        var first = endpoint.AvailableModelNames.First();
        var llmModelClient = new ModelSelectionViewModel(this.EndpointService)
        {
            SelectedModelName = first,
            SelectedEndpoint = endpoint
        }.GetClient();
        if (llmModelClient == null)
        {
            return;
        }

        if (multiResponseViewItem != null)
        {
            await AppendResponseOn(multiResponseViewItem, llmModelClient);
        }
    }));*/

    #region input box

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

    public long CurrentContextTokens
    {
        get
        {
            if (!DialogItems.Any())
            {
                return 0;
            }

            var dialogViewItems = new Stack<IDialogItem>();
            FilterHistory(DialogItems.ToArray(), dialogViewItems);
            return dialogViewItems.Sum(item => item.Tokens);
        }
    }

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

    public ICommand NewResponseCommand => new RelayCommand((async () =>
    {
        var prompt = PromptString?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }

        var requestViewItem = NewRequest(prompt);
        await NewResponse(requestViewItem);
    }));

    public ICommand CancelLastCommand => new ActionCommand((_ =>
    {
        var multiResponseViewItem = DialogItems.LastOrDefault() as MultiResponseViewItem;
        if (multiResponseViewItem?.AcceptedResponse is RespondingViewItem respondingViewItem)
        {
            respondingViewItem.CancelCommand.Execute(null);
        }
    }));

    public ICommand ConclusionCommand => new ActionCommand((o => { ConclusionCurrent(); }));

    #endregion

    #region scroll

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

    public ICommand ScrollToEndCommand => new ActionCommand((o =>
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
    }));

    public ICommand ScrollToBeginCommand => new ActionCommand((o =>
    {
        if (!DialogItems.Any())
        {
            return;
        }

        this.ScrollViewItem = DialogItems.First();
    }));

    #endregion

    #region attachment

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
            this.Attachments.Add(new Attachment()
            {
                Type = AttachmentType.Image,
                OriUri = new Uri(fileName)
            });
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

    private IEndpointService EndpointService => ServiceLocator.GetService<IEndpointService>()!;

    public IPromptsResource PromptsResource
    {
        get { return ServiceLocator.GetService<IPromptsResource>()!; }
    }

    private ILLMClient _defaultClient;

    public ILLMClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            _defaultClient = value;
            OnPropertyChanged();
            TrackClientChanged(value);
        }
    }

    private string? _promptString;
    private string _topic;
    private IDialogItem? _scrollViewItem;
    private bool _isNewResponding;
    private int _respondingCount;
    private MultiResponseViewItem? _focusedResponse;
    private bool _isChaining;
    private int _chainStepCount;
    private int _chainingStep;
    private bool _mcpEnabled;
    private string? _dialogPrompt;

    public DialogViewModel(string topic, ILLMClient modelClient,
        IList<IDialogItem>? items = null)
    {
        this._topic = topic;
        _defaultClient = modelClient;
        TrackClientChanged(modelClient);
        DialogItems = items == null ? [] : new ObservableCollection<IDialogItem>(items);
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
    }

    public async void ConclusionCurrent()
    {
        var dialogViewItems = this.DialogItems;
        var newGuid = Guid.NewGuid();
        var config = GlobalConfig.LoadOrCreate();
        var requestViewItem = new RequestViewItem()
        {
            TextMessage = string.Format(config.TokenSummarizePrompt, config.SummarizeWordsCount),
            InteractionId = newGuid
        };

        dialogViewItems.Add(requestViewItem);
        var copy = dialogViewItems.ToArray();
        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = newGuid };
        dialogViewItems.Add(multiResponseViewItem);
        ScrollViewItem = dialogViewItems.LastOrDefault();
        var completedResult = await SendRequestCore(this.DefaultClient, copy, multiResponseViewItem, this.DialogPrompt);
        if (completedResult.IsInterrupt)
        {
            dialogViewItems.Remove(multiResponseViewItem);
            dialogViewItems.Remove(requestViewItem);
            return;
        }

        var indexOf = dialogViewItems.IndexOf(requestViewItem);
        dialogViewItems.Insert(indexOf, new EraseViewItem());
        ScrollViewItem = dialogViewItems.LastOrDefault();
    }

    public async Task AppendResponseOn(MultiResponseViewItem responseViewItem,
        ILLMClient client)
    {
        //获得之前的所有请求
        var indexOf = DialogItems.IndexOf(responseViewItem);
        if (indexOf < 1)
        {
            return;
        }

        var dialogViewItems = DialogItems.Take(indexOf).ToArray();
        await SendRequestCore(client, dialogViewItems, responseViewItem, this.DialogPrompt);
    }

    public void DeleteItem(IDialogItem item)
    {
        var indexOf = this.DialogItems.IndexOf(item);
        if (item is RequestViewItem requestViewItem)
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

    private RequestViewItem NewRequest(string promptString)
    {
        IList<IAIFunctionGroup>? tools = null;
        if (this.DefaultClient.Model.SupportFunctionCall && this.MCPEnabled)
        {
            tools = this.SelectedFunctions;
        }

        return new RequestViewItem()
        {
            InteractionId = Guid.NewGuid(),
            TextMessage = promptString.Trim(),
            Attachments = Attachments.ToList(),
            FunctionGroups = tools,
        };
    }

    public async Task<CompletedResult> SendRequestCore(ILLMClient client,
        Memory<IDialogItem> dialog, MultiResponseViewItem multiResponseViewItem, string? systemPrompt = null)
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
            var list = GenerateHistory(dialog);
            completedResult = await client.SendRequest(list,
                cancellationToken: respondingViewItem.RequestTokenSource.Token, systemPrompt: systemPrompt);
            multiResponseViewItem.Append(new ResponseViewItem(client.Model, completedResult, client.Endpoint.Name));
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

        this.TokensConsumption += completedResult.Usage.TotalTokenCount ?? 0;
        this.TotalPrice += completedResult.Price ?? 0;
        OnPropertyChangedAsync(nameof(Shortcut));
        OnPropertyChangedAsync(nameof(CurrentContextTokens));
        return completedResult;
    }

    public async Task NewResponse(RequestViewItem requestViewItem)
    {
        DialogItems.Add(requestViewItem);
        var copy = DialogItems.ToArray();
        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = requestViewItem.InteractionId };
        DialogItems.Add(multiResponseViewItem);
        ScrollViewItem = DialogItems.Last();
        IsNewResponding = true;
        var completedResult = await SendRequestCore(this.DefaultClient, copy, multiResponseViewItem, this.DialogPrompt);
        IsNewResponding = false;
        if (!completedResult.IsInterrupt)
        {
            this.PromptString = string.Empty;
            this.Attachments.Clear();
        }
    }

    public async void ReBaseOn(RequestViewItem redoItem)
    {
        //删除之后的所有记录
        var indexOf = DialogItems.IndexOf(redoItem);
        var dialogCount = DialogItems.Count;
        for (int i = 0; i < dialogCount - indexOf; i++)
        {
            DialogItems.RemoveAt(indexOf);
        }

        await NewResponse(redoItem);
    }

    public async void ClearBefore(RequestViewItem requestViewItem)
    {
        if ((await DialogHost.Show(new ConfirmView() { Header = "清空会话？" })) is true)
        {
            var indexOf = DialogItems.IndexOf(requestViewItem);
            for (int i = 0; i < indexOf; i++)
            {
                DialogItems.RemoveAt(0);
            }

            this.ScrollViewItem = requestViewItem;
        }
    }

    public async void RetryCurrent(MultiResponseViewItem multiResponseViewItem)
    {
        // var index = multiResponseViewItem.AcceptedIndex;
        if (multiResponseViewItem.AcceptedResponse is not ResponseViewItem responseViewItem)
        {
            MessageEventBus.Publish("响应为空！");
            return;
        }

        var client = EndpointService.GetEndpoint(responseViewItem.EndPointName)?
            .NewClient(responseViewItem.ModelName);
        if (client == null)
        {
            MessageEventBus.Publish("已无法找到模型！");
            return;
        }

        multiResponseViewItem.Remove(responseViewItem);
        await AppendResponseOn(multiResponseViewItem, client);
    }

    protected static void FilterHistory(Span<IDialogItem> source, Stack<IDialogItem> dialogViewItems)
    {
        Guid? interactionId = null;
        for (int i = source.Length - 1; i >= 0; i--)
        {
            var dialogViewItem = source[i];
            if (dialogViewItem is EraseViewItem)
            {
                break;
            }

            if (dialogViewItem is MultiResponseViewItem multiResponseViewItem)
            {
                if (multiResponseViewItem.IsAvailableInContext)
                {
                    interactionId = multiResponseViewItem.InteractionId;
                    dialogViewItems.Push(multiResponseViewItem);
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
                    dialogViewItems.Push(requestViewItem);
                }
            }
        }
    }

    private static IList<IDialogItem> GenerateHistory(Memory<IDialogItem> memory)
    {
        var dialogViewItems = new Stack<IDialogItem>();
        var source = memory.Span;
        var lastRequest = source[^1];
        if (lastRequest is not RequestViewItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        dialogViewItems.Push(lastRequest);
        FilterHistory(source.Slice(0, source.Length - 1), dialogViewItems);
        var list = new List<IDialogItem>();
        while (dialogViewItems.TryPop(out var dialogViewItem))
        {
            list.Add(dialogViewItem);
        }

        return list;
    }

    private void TagDataChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }

    private void TrackClientChanged(ILLMClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChangedOnPropertyChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChangedOnPropertyChanged;
        }
    }

    public void InsertClearContextItem(IDialogItem item)
    {
        var indexOf = DialogItems.IndexOf(item);
        DialogItems.Insert(indexOf, new EraseViewItem());
    }

    private void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChangedAsync(nameof(Shortcut));
        OnPropertyChanged(nameof(CurrentContextTokens));
    }
}