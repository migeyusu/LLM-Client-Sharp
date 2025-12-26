using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class MultiResponseViewItem : BaseViewModel, IDialogItem
{
    public Guid InteractionId { get; set; }

    public DialogSessionViewModel ParentSession { get; }

    public async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (AcceptedResponse == null)
        {
            yield break;
        }

        await foreach (var chatMessage in AcceptedResponse.GetMessagesAsync(cancellationToken))
        {
            yield return chatMessage;
        }
    }

    /// <summary>
    /// warning: 禁止用于绑定，因为没有实现属性通知
    /// </summary>
    public bool IsAvailableInContext
    {
        get { return AcceptedResponse?.IsAvailableInContext == true; }
    }

    public bool IsResponding
    {
        get { return Items.Any(item => item.IsResponding); }
    }

    public bool HasAvailableMessage
    {
        get { return Items.Any((item => item.IsAvailableInContext)); }
    }

    public long Tokens
    {
        get { return AcceptedResponse?.Tokens ?? 0; }
    }

    public bool IsMultiResponse
    {
        get => _isMultiResponse;
        set
        {
            if (value == _isMultiResponse) return;
            _isMultiResponse = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ResponseViewItem> Items
    {
        get => _items;
        set
        {
            if (Equals(value, _items)) return;
            _items = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAvailableMessage));
            OnPropertyChanged(nameof(AcceptedResponse));
            OnPropertyChanged(nameof(RemoveCommand));
        }
    }

    private bool _isMultiResponse = false;
    private ObservableCollection<ResponseViewItem> _items;

    private ResponseViewItem? _acceptedResponse;

    public ModelSelectionPopupViewModel SelectionPopup { get; }

    public ICommand RebaseCommand => new ActionCommand(o => { ParentSession.RemoveAfter(this); });

    public ICommand ClearOthersCommand { get; }

    //标记为有效结果
    public ICommand MarkValid => new ActionCommand((o =>
    {
        if (this.AcceptedResponse != null)
        {
            this.AcceptedResponse.IsManualValid = true;
        }
    }));

    public ICommand RefreshSelectedCommand => new ActionCommand(o => RetryCurrent());

    public ICommand SetAsAvailableCommand => new ActionCommand(o =>
    {
        if (o is ResponseViewItem response)
        {
            response.SwitchAvailableInContext();
        }
    });

    public ResponseViewItem? AcceptedResponse
    {
        get => _acceptedResponse;
        set
        {
            if (Equals(value, _acceptedResponse)) return;
            _acceptedResponse = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
            OnPropertyChanged(nameof(MarkValid));
            OnPropertyChanged(nameof(EditCommand));
        }
    }

    public ICommand RemoveCommand => new ActionCommand(o =>
    {
        if (Items.Count == 1)
        {
            return;
        }

        if (o is ResponseViewItem response)
        {
            this.Remove(response);
        }
    });

    public ICommand EditCommand => new ActionCommand(o =>
    {
        if (AcceptedResponse is { } response)
        {
            var editView = response.EditViewModel;
            DialogHost.Show(editView);
        }
    });

    public ICommand CopyInteractionCommand { get; }

    public ICommand PasteInteractionCommand => new ActionCommand(o =>
    {
        var indexOf = this.ParentSession.DialogItems.IndexOf(this);
        this.ParentSession.PasteInteraction(indexOf + 1);
    });

    public MultiResponseViewItem(IEnumerable<ResponseViewItem> items, DialogSessionViewModel parentSession)
    {
        ParentSession = parentSession;
        _items = new ObservableCollection<ResponseViewItem>(items);
        _items.CollectionChanged += (sender, args) => { this.ParentSession.IsDataChanged = true; };
        IsMultiResponse = Items.Count > 1;
        SelectionPopup = new ModelSelectionPopupViewModel(client => { this.NewRequest(client.CreateClient()); })
        {
            SuccessRoutedCommand = PopupBox.ClosePopupCommand
        };
        CopyInteractionCommand = new ActionCommand(o => { this.ParentSession.CopyInteraction(this); });
        ClearOthersCommand = new RelayCommand(() =>
        {
            var toRemove = Items.Where(item => item != AcceptedResponse).ToList();
            foreach (var item in toRemove)
            {
                this.Remove(item);
            }
        });
    }

    public MultiResponseViewItem(DialogSessionViewModel parentSession) : this([], parentSession)
    {
    }

    public async void RetryCurrent()
    {
        // var index = multiResponseViewItem.AcceptedIndex;
        var responseViewItem = this.AcceptedResponse;
        if (responseViewItem == null)
        {
            MessageBox.Show("未选择响应！");
            return;
        }

        var dialogContext = ParentSession.CreateDialogContextBefore(this);
        await ParentSession.InvokeRequest(() => responseViewItem.SendRequest(dialogContext));
    }

    public Task<CompletedResult> NewRequest(ILLMChatClient chatClient, CancellationToken token = default)
    {
        var responseViewItem = new ResponseViewItem(chatClient);
        this.Append(responseViewItem);
        var dialogContext = ParentSession.CreateDialogContextBefore(this);
        return ParentSession.InvokeRequest(() => responseViewItem.SendRequest(dialogContext, token));
    }

    public void Append(ResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedResponse = viewItem;
        this.IsMultiResponse = Items.Count > 1;
    }

    public void Insert(ResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedResponse = viewItem;
        this.IsMultiResponse = Items.Count > 1;
    }

    public void Remove(ResponseViewItem viewItem)
    {
        var indexOf = this.Items.IndexOf(viewItem);
        if (indexOf < 0)
        {
            return;
        }

        var removeAccepted = AcceptedResponse == viewItem;
        this.Items.RemoveAt(indexOf);
        this.IsMultiResponse = Items.Count > 1;
        if (removeAccepted)
        {
            this.AcceptedResponse = _items.FirstOrDefault();
        }
    }

    public void RemoveAt(int index)
    {
        var responseViewItem = this.Items[index];
        this.Items.RemoveAt(index);
        if (responseViewItem == AcceptedResponse)
        {
            this.AcceptedResponse = _items.FirstOrDefault();
        }

        this.IsMultiResponse = Items.Count > 1;
    }
}