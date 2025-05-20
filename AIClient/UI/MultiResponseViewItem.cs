using System.Collections.ObjectModel;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;

namespace LLMClient.UI;

public interface IResponse
{
    long Tokens { get; }

    /// <summary>
    /// The latency of the response in ms
    /// </summary>
    int Latency { get; }

    /// <summary>
    /// The duration of the response in s
    /// </summary>
    int Duration { get; }

    string? Raw { get; }

    bool IsInterrupt { get; }

    string? ErrorMessage { get; }
}

public interface IResponseViewItem : IResponse, IDialogViewItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }
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
        get { return Items.Any(item => item.IsAvailableInContext); }
    }

    public long Tokens
    {
        get { return AcceptedResponse?.Tokens ?? 0; }
    }

    public int Latency
    {
        get { return AcceptedResponse?.Latency ?? 0; }
    }

    public int Duration
    {
        get { return AcceptedResponse?.Duration ?? 0; }
    }

    public string? Raw
    {
        get { return AcceptedResponse?.Raw; }
    }

    public bool IsInterrupt
    {
        get { return AcceptedResponse?.IsInterrupt ?? false; }
    }

    public string? ErrorMessage
    {
        get { return AcceptedResponse?.ErrorMessage; }
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
            OnPropertyChanged(nameof(IsAvailableInContext));
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