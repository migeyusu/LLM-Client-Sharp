using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

/// <summary>
/// 只保留最小功能的对话
/// </summary>
public class DialogCoreViewModel : NotifyDataErrorInfoViewModelBase
{
    public ObservableCollection<IDialogItem> DialogItems { get; }

    private ILLMClient _defaultClient = NullLlmModelClient.Instance;

    public virtual ILLMClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            _defaultClient = value;
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


    private string? _systemPrompt;

    /// <summary>
    /// dialog level prompt
    /// </summary>
    public string? SystemPrompt
    {
        get => _systemPrompt;
        set
        {
            if (value == _systemPrompt) return;
            _systemPrompt = value;
            OnPropertyChanged();
        }
    }

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

    #region function call

    private bool _mcpEnabled;

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

    #region input box

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

    #region core method

    protected IEndpointService EndpointService => ServiceLocator.GetService<IEndpointService>()!;
    
    private static IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

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

    public async Task<CompletedResult> SendRequestCore(ILLMClient client,
        Memory<IDialogItem> dialog, MultiResponseViewItem multiResponseViewItem)
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
                cancellationToken: respondingViewItem.RequestTokenSource.Token, systemPrompt: this.SystemPrompt);
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

        this.TokensConsumption += completedResult.Usage.TotalTokenCount ?? 0;
        this.TotalPrice += completedResult.Price ?? 0;
        return completedResult;
    }

    public void InsertClearContextItem(IDialogItem item)
    {
        var indexOf = DialogItems.IndexOf(item);
        DialogItems.Insert(indexOf, new EraseViewItem());
    }

    public virtual void DeleteItem(IDialogItem item)
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

    protected RequestViewItem NewRequest(string promptString)
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

    public virtual async Task NewResponse(RequestViewItem requestViewItem)
    {
        DialogItems.Add(requestViewItem);
        var copy = DialogItems.ToArray();
        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = requestViewItem.InteractionId };
        DialogItems.Add(multiResponseViewItem);
        ScrollViewItem = DialogItems.Last();
        IsNewResponding = true;
        var completedResult = await SendRequestCore(this.DefaultClient, copy, multiResponseViewItem);
        IsNewResponding = false;
        if (!completedResult.IsInterrupt)
        {
            this.PromptString = string.Empty;
            this.Attachments.Clear();
        }
    }

    public async Task AppendResponseOn(MultiResponseViewItem responseViewItem, ILLMClient client)
    {
        //获得之前的所有请求
        var indexOf = DialogItems.IndexOf(responseViewItem);
        if (indexOf < 1)
        {
            return;
        }

        var dialogViewItems = DialogItems.Take(indexOf).ToArray();
        await SendRequestCore(client, dialogViewItems, responseViewItem);
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
        var completedResult = await SendRequestCore(this.DefaultClient, copy, multiResponseViewItem);
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

        var client = responseViewItem.Client;
        if (client == null)
        {
            MessageEventBus.Publish("已无法找到模型！");
            return;
        }

        multiResponseViewItem.Remove(responseViewItem);
        await AppendResponseOn(multiResponseViewItem, client);
    }

    #endregion

    public DialogCoreViewModel(IList<IDialogItem>? dialogItems = null)
    {
        DialogItems = dialogItems == null
            ? []
            : new ObservableCollection<IDialogItem>(dialogItems);
    }
}

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

public class AdditionalModelSelection : ModelSelectionViewModel
{
    public MultiResponseViewItem MultiResponseViewItem { get; }

    public AdditionalModelSelection(IEndpointService endpointService, MultiResponseViewItem multiResponseViewItem)
        : base(endpointService)
    {
        MultiResponseViewItem = multiResponseViewItem;
    }

    public ICommand AcceptModelCommand => new ActionCommand((async o =>
    {
        if (SelectedModel == null)
        {
            return;
        }

        var client = this.GetClient();
        if (client == null)
        {
            return;
        }

        if (o is DialogViewModel dialogViewModel)
        {
            await dialogViewModel.AppendResponseOn(this.MultiResponseViewItem, client);
        }

        if (o is FrameworkElement frameworkElement)
        {
            PopupBox.ClosePopupCommand.Execute(this, frameworkElement);
        }
    }));
}