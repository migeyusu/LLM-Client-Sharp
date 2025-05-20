// #define TESTMODE

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
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
    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (value == _isProcessing) return;
            _isProcessing = value;
            OnPropertyChanged();
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
            // PromptTokensCount = value?.Length ?? 0;
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
            OnPropertyChanged(nameof(SendRequestCommand));
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

    public ICommand TestCommand => new ActionCommand((async o =>
    {
        if (this.Client == null)
        {
            return;
        }

        await this.Client.AddFileToContext(new FileInfo("E:\\PCCT原型机文档\\PCCT项目DAS与DPB接口定义_1_6.docx"));
        
        var dialogViewItem = this.DialogItems.Last(item => item is MultiResponseViewItem && item.IsAvailableInContext);
        var multiResponseViewItem = dialogViewItem as MultiResponseViewItem;
        var endpoint = EndpointService.AvailableEndpoints[0];
        var first = endpoint.AvailableModelNames.First();
        if (multiResponseViewItem != null)
        {
            await AppendResponseOn(multiResponseViewItem, new ModelSelectionViewModel()
            {
                SelectedModelName = first,
                SelectedEndpoint = endpoint
            });
        }
    }));

    public ICommand SendRequestCommand => new RelayCommand((() =>
    {
        if (string.IsNullOrEmpty(PromptString?.Trim()))
        {
            return;
        }

        SendRequest(PromptString);
    }), () => { return Client != null; });

    public ICommand CancelCommand => new ActionCommand((o => { _requestTokenSource?.Cancel(); }));

    // public ICommand DeleteCommand => new ActionCommand((o => { this.DeleteItem((o as ResponseViewItem)!); }));

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
            this.ScrollViewItem = this.DialogItems.FirstOrDefault();
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

    public IEndpointService EndpointService { get; }

    private string? _promptString;
    private string _topic;
    private IDialogViewItem? _scrollViewItem;
    private CancellationTokenSource? _requestTokenSource;
    private DateTime _editTime = DateTime.Now;
    private long _tokensConsumption;
    private bool _isProcessing;

    public DialogViewModel(string topic, IEndpointService endpointService, ILLMModelClient? modelClient = null,
        IList<IDialogViewItem>? items = null)
    {
        this._topic = topic;
        EndpointService = endpointService;
        this.Client = modelClient;
        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScrollViewItem))
            {
                return;
            }

            IsDataChanged = true;
        };
        DialogItems = items == null ? [] : new ObservableCollection<IDialogViewItem>(items);
        this.DialogItems.CollectionChanged += DialogOnCollectionChanged;
    }

    public async Task AppendResponseOn(MultiResponseViewItem responseViewItem,
        ModelSelectionViewModel modelSelectionViewModel)
    {
        var llmModelClient = modelSelectionViewModel.GetClient();
        if (llmModelClient == null)
            return;
        //获得之前的所有请求
        var indexOf = DialogItems.IndexOf(responseViewItem);
        if (indexOf < 1)
        {
            return;
        }

        _requestTokenSource = new CancellationTokenSource();
        var dialogViewItems = DialogItems.Take(indexOf).ToArray();
        var respondingViewItem = new RespondingViewItem(llmModelClient);
        responseViewItem.Append(respondingViewItem);
        await SendRequestCore(_requestTokenSource, llmModelClient, dialogViewItems, responseViewItem);
        responseViewItem.Remove(respondingViewItem);
    }

    public void DeleteItem(IDialogViewItem item)
    {
        this.DialogItems.Remove(item);
        if (ScrollViewItem == item)
        {
            ScrollViewItem = this.DialogItems.LastOrDefault();
        }
    }

    public async Task<CompletedResult> SendRequestCore(CancellationTokenSource tokenSource,
        ILLMModelClient client, IList<IDialogViewItem> dialog,
        MultiResponseViewItem multiResponseViewItem)
    {
        var completedResult = CompletedResult.Empty;
        IsProcessing = true;
        try
        {
            var list = GenerateHistory(dialog);
            completedResult = await client.SendRequest(list, tokenSource.Token);
            multiResponseViewItem.Append(new ResponseViewItem(client.Info, completedResult, client.Endpoint.Name));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "请求失败", MessageBoxButton.OK, MessageBoxImage.Error);
            completedResult.ErrorMessage = exception.Message;
        }
        finally
        {
            tokenSource.Dispose();
        }

        IsProcessing = false;
        this.TokensConsumption += completedResult.Usage.TotalTokenCount ?? 0;
        OnPropertyChangedAsync(nameof(Shortcut));
        return completedResult;
    }

    public async void SendRequest(string prompt)
    {
        if (Client == null)
        {
            return;
        }

        var newGuid = Guid.NewGuid();
        var requestViewItem = new RequestViewItem() { MessageContent = prompt, InteractionId = newGuid };
        DialogItems.Add(requestViewItem);
        var copy = DialogItems.ToArray();
        _requestTokenSource = new CancellationTokenSource();
        var respondingViewItem = new RespondingViewItem(this.Client);
        var multiResponseViewItem = new MultiResponseViewItem([respondingViewItem]) { InteractionId = newGuid };
        DialogItems.Add(multiResponseViewItem);
        ScrollViewItem = DialogItems.Last();
        var completedResult = await SendRequestCore(_requestTokenSource, this.Client, copy,
            multiResponseViewItem);
        if (!completedResult.IsInterrupt)
        {
            this.PromptString = string.Empty;
        }

        multiResponseViewItem.Remove(respondingViewItem);
    }


    public void ReSend(RequestViewItem redoItem)
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
        SendRequest(message);
    }

    private static IList<IDialogViewItem> GenerateHistory(IList<IDialogViewItem> source)
    {
        var dialogViewItems = new Stack<IDialogViewItem>();
        var lastRequest = source[^1];
        if (lastRequest is not RequestViewItem)
        {
            throw new InvalidOperationException("最后一条记录不是请求");
        }

        dialogViewItems.Push(lastRequest);
        Guid? interactionId = null;
        for (int i = source.Count - 2; i >= 0; i--)
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
    }

    private void NotifyPropertyChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }
}