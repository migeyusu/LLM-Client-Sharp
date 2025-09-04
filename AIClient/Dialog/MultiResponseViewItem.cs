using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class MultiResponseViewItem : BaseViewModel, IDialogItem, IModelSelection
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

    public ObservableCollection<IResponseViewItem> Items
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

    private int _acceptedIndex = -1;
    private bool _isMultiResponse = false;
    private ObservableCollection<IResponseViewItem> _items;
    private ILLMChatModel? _selectedModel;

    public ILLMChatModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (Equals(value, _selectedModel)) return;
            _selectedModel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// used for append new response
    /// </summary>
    public ICommand SubmitCommand => new ActionCommand(async o =>
    {
        var llmClient = SelectedModel?.CreateChatClient();
        if (llmClient == null)
        {
            return;
        }

        if (o is FrameworkElement frameworkElement)
        {
            PopupBox.ClosePopupCommand.Execute(this, frameworkElement);
        }

        await ParentSession.AppendResponseOn(this, llmClient);
    });

    public ICommand RebaseCommand => new ActionCommand(o => { ParentSession.RemoveAfter(this); });

    //标记为有效结果
    public ICommand MarkValid => new ActionCommand((o =>
    {
        if (this.AcceptedResponse is ResponseViewItem responseViewItem)
        {
            responseViewItem.IsManualValid = true;
        }
    }));

    public ICommand RefreshSelectedCommand => new ActionCommand(o => RetryCurrent());

    public int AcceptedIndex
    {
        get => _acceptedIndex;
        set
        {
            if (value == _acceptedIndex) return;
            _acceptedIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AcceptedResponse));
        }
    }

    public ICommand SetAsAvailableCommand => new ActionCommand(o =>
    {
        if (o is IResponseViewItem response)
        {
            if (response is ResponseViewItem responseViewItem)
            {
                responseViewItem.SwitchAvailableInContext();
            }
            else if (response is RespondingViewItem)
            {
                MessageEventBus.Publish("正在回复！");
            }
        }
    });


    public ResponseViewItem? CurrentResponse
    {
        get
        {
            if (AcceptedResponse is ResponseViewItem response)
            {
                return response;
            }

            return null;
        }
    }

    public IResponseViewItem? AcceptedResponse
    {
        get
        {
            if (Items.Count == 0)
            {
                return null;
            }

            if (Items.Count == 1)
            {
                return Items[0];
            }

            if (AcceptedIndex < 0)
            {
                AcceptedIndex = 0;
            }

            return Items[AcceptedIndex];
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
        if (AcceptedResponse is ResponseViewItem response)
        {
            var editView = response.EditViewModel;
            DialogHost.Show(editView);
        }
    });
    
    public ICommand CopyInteractionCommand => new ActionCommand(o =>
    {
        this.ParentSession.CopyInteraction(this);
    });
    
    public ICommand PasteInteractionCommand => new ActionCommand(o =>
    {
        var indexOf = this.ParentSession.DialogItems.IndexOf(this);
        this.ParentSession.PasteInteraction(indexOf + 1);
    });

    public MultiResponseViewItem(IEnumerable<IResponseViewItem> items, DialogSessionViewModel parentSession)
    {
        ParentSession = parentSession;
        _items = new ObservableCollection<IResponseViewItem>(items);
        IsMultiResponse = Items.Count > 1;
    }

    public MultiResponseViewItem(DialogSessionViewModel parentSession) : this([], parentSession)
    {
    }

    public async void RetryCurrent()
    {
        // var index = multiResponseViewItem.AcceptedIndex;
        if (this.AcceptedResponse is not ResponseViewItem responseViewItem)
        {
            MessageEventBus.Publish("未选择响应！");
            return;
        }

        var client = responseViewItem.Client;
        if (client == null)
        {
            MessageEventBus.Publish("已无法找到模型！");
            return;
        }

        this.Remove(responseViewItem);
        await ParentSession.AppendResponseOn(this, client);
    }

    public void Append(IResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedIndex = this.Items.Count - 1;
        IsMultiResponse = Items.Count > 1;
    }

    public void Insert(IResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedIndex = index;
        IsMultiResponse = Items.Count > 1;
    }

    public void Remove(IResponseViewItem viewItem)
    {
        var indexOf = this.Items.IndexOf(viewItem);
        if (indexOf < 0)
        {
            return;
        }

        var acceptedIndex = AcceptedIndex;
        this.Items.RemoveAt(indexOf);
        if (acceptedIndex >= indexOf)
        {
            this.AcceptedIndex = acceptedIndex - 1;
        }

        IsMultiResponse = Items.Count > 1;
    }

    public void RemoveAt(int index)
    {
        this.Items.RemoveAt(index);
        this.AcceptedIndex = AcceptedIndex - 1;
        IsMultiResponse = Items.Count > 1;
    }
}