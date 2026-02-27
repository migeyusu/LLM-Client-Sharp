using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LambdaConverters;
using LLMClient.Abstraction;
using LLMClient.Component.UserControls;
using LLMClient.Component.ViewModel;
using LLMClient.Endpoints;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog.Models;

public class MultiResponseViewItem : BaseDialogItem, ISearchableDialogItem, IInteractionItem, IEditableDialogItem
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

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
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
    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool IsAvailableInContext
    {
        get { return AcceptedResponse?.IsAvailableInContext == true; }
    }

    public void TriggerTextContentUpdate()
    {
        AcceptedResponse?.TriggerTextContentUpdate();
    }

    public bool IsResponding
    {
        get { return Items.Any(item => item.IsResponding); }
    }

    public bool HasAvailableMessage
    {
        get { return Items.Any(item => item.IsAvailableInContext); }
    }

    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override long Tokens
    {
        get { return AcceptedResponse?.Tokens ?? 0; }
    }

    public override ChatRole Role { get; } = ChatRole.Assistant;

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

    [Bindable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SearchableDocument? SearchableDocument
    {
        get { return AcceptedResponse?.SearchableDocument; }
    }

    public ObservableCollection<ResponseViewItem> Items { get; }

    public static readonly IMultiValueConverter DeleteItemConverter =
        MultiValueConverter.Create<bool, Visibility>(args =>
        {
            var values = args.Values;
            return !values[0] && values[1] && values[1] ? Visibility.Visible : Visibility.Collapsed;
        });

    public static ICommand CompareAllCommand { get; } = new RelayCommand<MultiResponseViewItem>((item =>
    {
        if (item == null)
        {
            return;
        }

        if (!item.Items.Any())
        {
            return;
        }

        var compareWindow = new MultiResponseCompareWindow(item.Items);
        compareWindow.ShowDialog();
    }));

    private bool _isMultiResponse = false;

    private bool _canGoToNext;
    private Guid _interactionId;
    private bool _canGotoPrevious;

    public ModelSelectionPopupViewModel SelectionPopup { get; }

    public ICommand ClearOthersCommand { get; }

    public ICommand RetryCommand { get; }

    private int _acceptedIndex = -1;

    public int AcceptedIndex
    {
        get => _acceptedIndex;
        set
        {
            if (value == _acceptedIndex) return;
            _acceptedIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AcceptedResponse));
            OnPropertyChanged(nameof(IsAvailableInContext));
            OnPropertyChanged(nameof(Tokens));
            OnPropertyChanged(nameof(SearchableDocument));
            CanGotoPrevious = value - 1 >= 0;
            CanGoToNext = value + 1 < Items.Count;
        }
    }

    public ResponseViewItem? AcceptedResponse
    {
        get
        {
            if (AcceptedIndex < 0 || AcceptedIndex >= Items.Count)
            {
                return Items.FirstOrDefault();
            }

            return Items[AcceptedIndex];
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

    public MultiResponseViewItem(IEnumerable<ResponseViewItem> items, DialogSessionViewModel parentSession)
    {
        ParentSession = parentSession;
        Items = new ObservableCollection<ResponseViewItem>(items);
        Items.CollectionChanged += (_, _) =>
        {
            this.ParentSession.IsDataChanged = true;
            this.IsMultiResponse = Items.Count > 1;
        };
        IsMultiResponse = Items.Count > 1;
        SelectionPopup = new ModelSelectionPopupViewModel(client => { this.AppendResponse(client.CreateClient()); })
        {
            SuccessRoutedCommand = PopupBox.ClosePopupCommand
        };
        RetryCommand = new RelayCommand<ResponseViewItem>(async void (responseViewItem) =>
        {
            try
            {
                if (responseViewItem == null)
                {
                    return;
                }

                if (responseViewItem.IsResponding)
                {
                    return;
                }

                await ParentSession.InvokeRequest(responseViewItem, this);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
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

            this.AcceptedIndex = index + 1;
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

            this.AcceptedIndex = index - 1;
        });
    }

    public MultiResponseViewItem(DialogSessionViewModel parentSession) : this([], parentSession)
    {
    }

    /*public Task<CompletedResult> InvokeRequest(ResponseViewItem responseViewItem)
    {
        var dialogItems = this.GetChatHistory().ToArray();
        var dialogContext = new DialogContext(dialogItems, this.ParentSession.SystemPrompt);
        return responseViewItem.ProcessRequest(dialogContext);
    }*/

    public Task<CompletedResult> AppendResponse(ILLMChatClient chatClient, CancellationToken token = default)
    {
        var responseViewItem = new ResponseViewItem(chatClient);
        this.AppendResponse(responseViewItem);
        return ParentSession.InvokeRequest(responseViewItem, this);
    }

    public void AppendResponse(ResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedIndex = this.Items.Count - 1;
        this.IsMultiResponse = Items.Count > 1;
    }

    public void Insert(ResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedIndex = index;
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
        if (removeAccepted)
        {
            this.AcceptedIndex = 0;
        }

        this.IsMultiResponse = Items.Count > 1;
    }

    public void RemoveAt(int index)
    {
        this.Items.RemoveAt(index);
        if (index >= this.Items.Count)
        {
            this.AcceptedIndex = this.Items.Count - 1;
        }

        this.IsMultiResponse = Items.Count > 1;
    }
}