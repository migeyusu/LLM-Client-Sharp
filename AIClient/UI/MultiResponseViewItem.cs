using System.Collections.ObjectModel;
using System.Windows.Media;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.UI;

public interface IResponseViewItem : IDialogViewItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }

    long Tokens { get; }
}

public class MultiResponseViewItem : BaseViewModel, IResponseViewItem
{
    public Guid InteractionId { get; set; }

    public ChatMessage? Message
    {
        get { return AcceptedResponse?.Message; }
    }

    public bool IsAvailableInContext
    {
        get { return AcceptedResponse?.IsAvailableInContext == true; }
    }

    public long Tokens
    {
        get { return AcceptedResponse?.Tokens ?? 0; }
    }

    public ThemedIcon Icon
    {
        get { return AcceptedResponse?.Icon ?? Icons.APIIcon; }
    }

    public string ModelName
    {
        get { return AcceptedResponse?.ModelName ?? String.Empty; }
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

    public ObservableCollection<IResponseViewItem> Items { get; }

    private int _acceptedIndex = 0;
    private bool _isMultiResponse = false;

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

    public IResponseViewItem? AcceptedResponse
    {
        get
        {
            /*if (SelectedIndex >= Items.Count||SelectedIndex == 0)
                throw new IndexOutOfRangeException();*/
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

    public MultiResponseViewItem(IEnumerable<IResponseViewItem> items)
    {
        Items = new ObservableCollection<IResponseViewItem>(items);
        /*((INotifyPropertyChanged)Items).PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Items.Count))
            {
                IsMultiResponse = Items.Count > 1;
            }
        };
        IsMultiResponse = Items.Count > 1;*/
        IsMultiResponse = Items.Count > 1;
    }

    public MultiResponseViewItem() : this(Enumerable.Empty<IResponseViewItem>())
    {
    }

    public void Append(IResponseViewItem viewItem)
    {
        this.Items.Add(viewItem);
        this.AcceptedIndex = this.Items.Count - 1;
        IsMultiResponse = Items.Count > 1;
    }

    public void Remove(IResponseViewItem viewItem)
    {
        this.Items.Remove(viewItem);
        this.AcceptedIndex = AcceptedIndex - 1;
        IsMultiResponse = Items.Count > 1;
    }
}