// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Document;
using Microsoft.SemanticKernel.Plugins.Document.FileSystem;
using Microsoft.SemanticKernel.Plugins.Document.OpenXml;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class DialogViewModel : BaseViewModel
{
    public DateTime EditTime
    {
        get => _editTime;
        set
        {
            if (value.Equals(_editTime)) return;
            _editTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 用于跟踪对话对象
    /// </summary>
    public string FileName { get; set; } = String.Empty;

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = false;

    /// <summary>
    /// 正在处理
    /// </summary>
    public bool IsResponding
    {
        get => RespondingCount > 0;
    }

    public bool IsNewResponding
    {
        get => _isNewResponding;
        set
        {
            if (value == _isNewResponding) return;
            _isNewResponding = value;
            OnPropertyChanged();
        }
    }

    public int RespondingCount
    {
        get => _respondingCount;
        set
        {
            if (value == _respondingCount) return;
            _respondingCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsResponding));
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

    public IDialogViewItem? ScrollViewItem
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

    public ObservableCollection<IDialogViewItem> DialogItems { get; }

    public long CurrentContextTokens
    {
        get
        {
            if (!DialogItems.Any())
            {
                return 0;
            }

            var dialogViewItems = new Stack<IDialogViewItem>();
            FilterHistory(DialogItems.ToArray(), dialogViewItems);
            return dialogViewItems.Sum(item => item.Tokens);
        }
    }

    private ILLMModelClient? _client;

    public ILLMModelClient? Client
    {
        get => _client;
        set
        {
            if (Equals(value, _client)) return;
            if (_client is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= NotifyPropertyChangedOnPropertyChanged;
            }

            if (_client?.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }

            _client = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewResponseCommand));
            if (value is INotifyPropertyChanged newValue)
            {
                newValue.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }

            if (_client?.Parameters is INotifyPropertyChanged newParameters)
            {
                newParameters.PropertyChanged += NotifyPropertyChangedOnPropertyChanged;
            }
        }
    }

    public string? Shortcut
    {
        get
        {
            return DialogItems.FirstOrDefault(item =>
            {
                return item is MultiResponseViewItem && item.IsAvailableInContext;
            })?.Message?.Text;
        }
    }

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

    private void CheckResponse(IList<MultiResponseViewItem> responseViewItems, ref int responseIndex)
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
        CheckResponse(responseViewItems, ref responseIndex);
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
        CheckResponse(responseViewItems, ref responseIndex);
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

    private IMapper Mapper => ServiceLocator.GetService<IMapper>()!;

    public ICommand BackupCommand => new ActionCommand((async o =>
    {
        var saveFileDialog = new SaveFileDialog()
        {
            AddExtension = true, DefaultExt = ".json", CheckPathExists = true,
            Filter = "json files (*.json)|*.json"
        };
        var dialogModel = Mapper.Map<DialogViewModel, DialogPersistModel>(this);
        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var fileName = saveFileDialog.FileName;
        var fileInfo = new FileInfo(fileName);
        await using (var fileStream = fileInfo.OpenWrite())
        {
            await JsonSerializer.SerializeAsync(fileStream, dialogModel);
        }

        MessageEventBus.Publish("已备份");
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
        if (this.Client != null)
        {
            stringBuilder.AppendLine($"### {this.Client.Name}");
        }

        foreach (var viewItem in this.DialogItems.Where((item => item.IsAvailableInContext)))
        {
            if (viewItem is IResponseViewItem responseViewItem)
            {
                stringBuilder.AppendLine("## **Assistant:**");
                stringBuilder.Append(responseViewItem.Raw);
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("***");
                stringBuilder.AppendLine();
            }
            else if (viewItem is RequestViewItem reqViewItem)
            {
                stringBuilder.AppendLine("## **User:**");
                stringBuilder.Append(reqViewItem.MessageContent);
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

    public ICommand NewResponseCommand => new RelayCommand((() =>
    {
        if (string.IsNullOrEmpty(PromptString?.Trim()))
        {
            return;
        }

        NewResponse(PromptString);
    }), () => { return Client != null; });

    public ICommand CancelCommand => new ActionCommand((o =>
    {
        var multiResponseViewItem = DialogItems.LastOrDefault() as MultiResponseViewItem;
        if (multiResponseViewItem?.AcceptedResponse is RespondingViewItem respondingViewItem)
        {
            respondingViewItem.CancelCommand.Execute(null);
        }
    }));

    public ICommand ScrollToPreviousCommand => new ActionCommand(o =>
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
            if (indexOf != -1 && indexOf < this.DialogItems.Count - 1)
            {
                this.ScrollViewItem = this.DialogItems[indexOf + 1];
            }
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

        this.ScrollViewItem = DialogItems.Last();
    }));

    public ICommand ScrollToBeginCommand => new ActionCommand((o =>
    {
        if (!DialogItems.Any())
        {
            return;
        }

        this.ScrollViewItem = DialogItems.First();
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

            this.Client = model;
        }
    }));

    public ICommand ConclusionCommand => new ActionCommand((o => { ConclusionTillEnd(); }));
    
    private IEndpointService EndpointService
    {
        get { return ServiceLocator.GetService<IEndpointService>()!; }
    }

    private string? _promptString;
    private string _topic;
    private IDialogViewItem? _scrollViewItem;

    private DateTime _editTime = DateTime.Now;
    private long _tokensConsumption;
    private bool _isNewResponding;
    private int _respondingCount;
    private MultiResponseViewItem? _focusedResponse;


    public DialogViewModel(string topic, ILLMModelClient? modelClient = null,
        IList<IDialogViewItem>? items = null)
    {
        this._topic = topic;
        this.Client = modelClient;
        this.PropertyChanged += (_, e) =>
        {
            var propertyName = e.PropertyName;
            if (propertyName == nameof(ScrollViewItem))
            {
                return;
            }

            IsDataChanged = true;
        };
        DialogItems = items == null ? [] : new ObservableCollection<IDialogViewItem>(items);
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
    }

    public async void ConclusionTillEnd()
    {
        if (this.Client == null)
        {
            return;
        }

        var dialogViewItems = this.DialogItems;
        var newGuid = Guid.NewGuid();
        var config = GlobalConfig.LoadOrCreate();
        var requestViewItem = new RequestViewItem()
        {
            MessageContent = string.Format(config.TokenSummarizePrompt, config.SummarizeWordsCount),
            InteractionId = newGuid
        };
        dialogViewItems.Add(requestViewItem);
        var copy = dialogViewItems.ToArray();
        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = newGuid };
        dialogViewItems.Add(multiResponseViewItem);
        ScrollViewItem = dialogViewItems.LastOrDefault();
        var completedResult = await SendRequestCore(this.Client, copy, multiResponseViewItem);
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
        ILLMModelClient llmModelClient)
    {
        //获得之前的所有请求
        var indexOf = DialogItems.IndexOf(responseViewItem);
        if (indexOf < 1)
        {
            return;
        }

        var dialogViewItems = DialogItems.Take(indexOf).ToArray();
        await SendRequestCore(llmModelClient, dialogViewItems, responseViewItem);
    }

    public void DeleteItem(IDialogViewItem item)
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

    public async Task<CompletedResult> SendRequestCore(
        ILLMModelClient client, Memory<IDialogViewItem> dialog,
        MultiResponseViewItem multiResponseViewItem)
    {
        var completedResult = CompletedResult.Empty;
        RespondingCount++;
        var respondingViewItem = new RespondingViewItem(client);
        try
        {
            multiResponseViewItem.Append(respondingViewItem);
            var list = GenerateHistory(dialog);
            completedResult = await client.SendRequest(list, respondingViewItem.RequestTokenSource.Token);
            multiResponseViewItem.Append(new ResponseViewItem(client.Info, completedResult, client.Endpoint.Name));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "请求失败", MessageBoxButton.OK, MessageBoxImage.Error);
            completedResult.ErrorMessage = exception.Message;
        }
        finally
        {
            respondingViewItem.RequestTokenSource.Dispose();
            multiResponseViewItem.Remove(respondingViewItem);
            RespondingCount--;
        }

        this.TokensConsumption += completedResult.Usage.TotalTokenCount ?? 0;
        OnPropertyChangedAsync(nameof(Shortcut));
        OnPropertyChangedAsync(nameof(CurrentContextTokens));
        return completedResult;
    }

    public async void NewResponse(string prompt)
    {
        if (Client == null)
        {
            return;
        }

        var newGuid = Guid.NewGuid();
        var requestViewItem = new RequestViewItem() { MessageContent = prompt, InteractionId = newGuid };
        DialogItems.Add(requestViewItem);
        var copy = DialogItems.ToArray();
        var multiResponseViewItem = new MultiResponseViewItem() { InteractionId = newGuid };
        DialogItems.Add(multiResponseViewItem);
        ScrollViewItem = DialogItems.Last();
        IsNewResponding = true;
        var completedResult = await SendRequestCore(this.Client, copy,
            multiResponseViewItem);
        IsNewResponding = false;
        if (!completedResult.IsInterrupt)
        {
            this.PromptString = string.Empty;
        }
    }


    public void ReBase(RequestViewItem redoItem)
    {
        if (Client == null)
        {
            return;
        }

        //删除本请求及之后的所有记录
        var indexOf = DialogItems.IndexOf(redoItem);
        var dialogCount = DialogItems.Count;
        for (int i = 0; i < dialogCount - indexOf; i++)
        {
            DialogItems.RemoveAt(indexOf);
        }

        var message = redoItem.MessageContent;
        PromptString = message;
        NewResponse(message);
    }

    public async void RetryCurrent(MultiResponseViewItem multiResponseViewItem)
    {
        // var index = multiResponseViewItem.AcceptedIndex;
        var responseViewItem = multiResponseViewItem.AcceptedResponse;
        if (responseViewItem == null)
        {
            MessageEventBus.Publish("响应为空！");
            return;
        }

        var llmModel = EndpointService.GetEndpoint(responseViewItem.EndPointName)?
            .NewClient(responseViewItem.ModelName);
        if (llmModel == null)
        {
            MessageEventBus.Publish("已无法找到模型！");
            return;
        }

        multiResponseViewItem.Remove(responseViewItem);
        await AppendResponseOn(multiResponseViewItem, llmModel);
    }

    private static void FilterHistory(Span<IDialogViewItem> source, Stack<IDialogViewItem> dialogViewItems)
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


    private static IList<IDialogViewItem> GenerateHistory(Memory<IDialogViewItem> memory)
    {
        var dialogViewItems = new Stack<IDialogViewItem>();
        var source = memory.Span;
        var lastRequest = source[^1];
        if (lastRequest is not RequestViewItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        dialogViewItems.Push(lastRequest);
        FilterHistory(source.Slice(0, source.Length - 1), dialogViewItems);
        var list = new List<IDialogViewItem>();
        while (dialogViewItems.TryPop(out var dialogViewItem))
        {
            list.Add(dialogViewItem);
        }

        return list;
    }

    public void InsertClearContextItem(IDialogViewItem item)
    {
        var indexOf = DialogItems.IndexOf(item);
        DialogItems.Insert(indexOf, new EraseViewItem());
    }

    private void DialogOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsDataChanged = true;
        this.EditTime = DateTime.Now;
        OnPropertyChangedAsync(nameof(Shortcut));
        OnPropertyChanged(nameof(CurrentContextTokens));
    }

    private void NotifyPropertyChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }
}