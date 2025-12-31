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
    public Guid InteractionId
    {
        get => _interactionId;
        set
        {
            if (value.Equals(_interactionId)) return;
            _interactionId = value;
            OnPropertyChanged();
        }
    }

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
            OnPropertyChanged(nameof(IsResponding));
            OnPropertyChanged(nameof(HasAvailableMessage));
            OnPropertyChanged(nameof(AcceptedResponse));
        }
    }

    private bool _isMultiResponse = false;
    private ObservableCollection<ResponseViewItem> _items;

    private ResponseViewItem? _acceptedResponse;
    private bool _canGoToNext;
    private Guid _interactionId;
    private bool _canGotoPrevious;

    public ModelSelectionPopupViewModel SelectionPopup { get; }

    public static ICommand RebaseCommand { get; } =
        new RelayCommand<MultiResponseViewItem>(o =>
        {
            if (o == null)
            {
                return;
            }

            o.ParentSession.RemoveAfter(o);
        });

    public ICommand ClearOthersCommand { get; }

    //标记为有效结果
    public static ICommand MarkValidCommand { get; } = new RelayCommand<MultiResponseViewItem>((o =>
    {
        if (o == null)
        {
            return;
        }

        var acceptedResponse = o.AcceptedResponse;
        if (acceptedResponse != null)
        {
            acceptedResponse.IsManualValid = true;
        }
    }));

    public static ICommand RetryCurrentCommand { get; } =
        new RelayCommand<MultiResponseViewItem>(o => o?.RetryCurrent());

    public ICommand SetAsAvailableCommand { get; }

    public ResponseViewItem? AcceptedResponse
    {
        get => _acceptedResponse;
        set
        {
            if (Equals(value, _acceptedResponse)) return;
            _acceptedResponse = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
            OnPropertyChanged(nameof(Tokens));
            var responseViewItems = this.Items;
            CanGotoPrevious = value != null &&
                              responseViewItems.IndexOf(value) - 1 >= 0;
            CanGoToNext = value != null &&
                          responseViewItems.IndexOf(value) + 1 < responseViewItems.Count;
        }
    }

    public bool CanGoToNext
    {
        get => _canGoToNext;
        set
        {
            if (value == _canGoToNext) return;
            _canGoToNext = value;
            OnPropertyChanged();
        }
    }

    public ICommand GotoNextCommand { get; }

    public bool CanGotoPrevious
    {
        get => _canGotoPrevious;
        set
        {
            if (value == _canGotoPrevious) return;
            _canGotoPrevious = value;
            OnPropertyChanged();
        }
    }

    public ICommand GotoPreviousCommand { get; }

    public ICommand RemoveResponseCommand { get; }

    public static ICommand CopyInteractionCommand { get; } =
        new RelayCommand<MultiResponseViewItem>(o =>
        {
            if (o == null)
            {
                return;
            }

            o.ParentSession.CopyInteraction(o);
        });

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
        RemoveResponseCommand = new RelayCommand<ResponseViewItem>(o =>
        {
            if (o == null)
            {
                return;
            }

            if (Items.Count == 1)
            {
                return;
            }

            this.RemoveResponse(o);
        });
        SetAsAvailableCommand = new ActionCommand(o =>
        {
            if (o is ResponseViewItem response)
            {
                response.SwitchAvailableInContext();
            }
        });

        ClearOthersCommand = new RelayCommand(() =>
        {
            var toRemove = Items.Where(item => item != AcceptedResponse).ToList();
            foreach (var item in toRemove)
            {
                this.RemoveResponse(item);
            }
        });
        GotoNextCommand = new RelayCommand(() =>
        {
            if (this.Items.Count == 0)
            {
                return;
            }

            var index = this.AcceptedResponse == null ? 0 : this.Items.IndexOf(this.AcceptedResponse);
            if (index < 0 || index + 1 >= this.Items.Count)
            {
                return;
            }

            this.AcceptedResponse = this.Items[index + 1];
        });
        GotoPreviousCommand = new RelayCommand(() =>
        {
            if (this.Items.Count == 0)
            {
                return;
            }

            var index = this.AcceptedResponse == null ? 0 : this.Items.IndexOf(this.AcceptedResponse);
            if (index <= 0)
            {
                return;
            }

            this.AcceptedResponse = this.Items[index - 1];
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

    public void RemoveResponse(ResponseViewItem viewItem)
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