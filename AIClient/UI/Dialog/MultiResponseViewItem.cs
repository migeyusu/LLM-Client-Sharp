using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public class MultiResponseViewItem : BaseViewModel, IDialogItem
{
    public Guid InteractionId { get; set; }

    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (AcceptedResponse == null)
        {
            yield break;
        }

        await foreach (var chatMessage in AcceptedResponse.GetMessages(cancellationToken))
        {
            yield return chatMessage;
        }
    }

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
        }
    }

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

    public MultiResponseViewItem(IEnumerable<IResponseViewItem> items)
    {
        _items = new ObservableCollection<IResponseViewItem>(items);
        IsMultiResponse = Items.Count > 1;
    }

    public MultiResponseViewItem() : this([])
    {
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