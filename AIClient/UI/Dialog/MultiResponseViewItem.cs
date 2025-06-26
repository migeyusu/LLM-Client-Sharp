using System.Collections.ObjectModel;
using System.Windows.Input;
using LLMClient.Data;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI.Dialog;

public interface IResponse : ITokenizable
{
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

    double? Price { get; }
}

public interface IResponseViewItem : IResponse, IDialogItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }

    string EndPointName { get; }
}

public class MultiResponseViewItem : BaseViewModel, IResponseViewItem
{
    public Guid InteractionId { get; set; }

    public Task<ChatMessage?> GetMessage()
    {
        if (AcceptedResponse == null)
        {
            return Task.FromResult<ChatMessage?>(null);
        }

        return AcceptedResponse.GetMessage();
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

    public double? Price
    {
        get { return Items.Sum((item => item.Price ?? 0)); }
    }

    public ThemedIcon Icon
    {
        get { return AcceptedResponse?.Icon ?? ImageExtensions.APIIcon; }
    }

    public string ModelName
    {
        get { return AcceptedResponse?.ModelName ?? String.Empty; }
    }

    public string EndPointName
    {
        get { return AcceptedResponse?.EndPointName ?? String.Empty; }
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

    private int _acceptedIndex = -1;
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
        OnPropertyChanged(nameof(Price));
    }

    public void Insert(IResponseViewItem viewItem, int index)
    {
        this.Items.Insert(index, viewItem);
        this.AcceptedIndex = index;
        IsMultiResponse = Items.Count > 1;
        OnPropertyChanged(nameof(Price));
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
        OnPropertyChanged(nameof(Price));
    }

    public void RemoveAt(int index)
    {
        this.Items.RemoveAt(index);
        this.AcceptedIndex = AcceptedIndex - 1;
        IsMultiResponse = Items.Count > 1;
        OnPropertyChanged(nameof(Price));
    }
}