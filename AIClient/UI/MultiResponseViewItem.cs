using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class MultiResponseViewItem : BaseViewModel, IDialogViewItem
{
    public ChatMessage? Message
    {
        get { return AcceptedResponse.Message; }
    }

    public bool IsAvailableInContext
    {
        get { return AcceptedResponse.IsAvailableInContext; }
    }

    public long Tokens
    {
        get { return AcceptedResponse.Tokens; }
    }

    public ObservableCollection<ResponseViewItem> Items { get; set; } = new ObservableCollection<ResponseViewItem>();

    private int _acceptedIndex = 0;

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

    public ResponseViewItem AcceptedResponse
    {
        get
        {
            /*if (SelectedIndex >= Items.Count||SelectedIndex == 0)
                throw new IndexOutOfRangeException();*/
            return Items[AcceptedIndex];
        }
    }
}