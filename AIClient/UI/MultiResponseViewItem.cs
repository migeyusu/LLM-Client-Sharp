using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

/// <summary>
/// 多回复
/// </summary>
public class MultiResponseViewItem: BaseViewModel,IDialogViewItem
{
    private int _acceptedIndex = 0;

    [JsonIgnore]
    public ChatMessage? Message {
        get
        {
            return AcceptedResponse.Message;
        } }

    [JsonIgnore]
    public bool IsAvailableInContext
    {
        get { return AcceptedResponse.IsAvailableInContext; }
    }

    [JsonIgnore]
    public long Tokens {
        get
        {
            return AcceptedResponse.Tokens;
        } }

    public ObservableCollection<ResponseViewItem> Items { get; set; } = new ObservableCollection<ResponseViewItem>();

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

    [JsonIgnore] 
    public ResponseViewItem AcceptedResponse {
        get
        {
            /*if (SelectedIndex >= Items.Count||SelectedIndex == 0)
                throw new IndexOutOfRangeException();*/
            return Items[AcceptedIndex];
        } }

    public ICommand AcceptCommand => new ActionCommand(o =>
    {
        if (o is int index)
        {
            this.AcceptedIndex = index;
        }
    });
    
}