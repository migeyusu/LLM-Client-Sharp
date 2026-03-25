using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public partial class MultiResponseCompareWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<ComparableResponseViewItem> ResponseViewItems { get; }

    public MultiResponseCompareWindow(IList<ClientResponseViewItem> items)
    {
        ResponseViewItems =
            new ObservableCollection<ComparableResponseViewItem>(items.Select((item =>
                new ComparableResponseViewItem(item))));
        this.DataContext = this;
        InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void DeleteItemCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ComparableResponseViewItem item)
        {
            ResponseViewItems.Remove(item);
        }
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is Thumb thumb)
        {
            if (thumb.DataContext is ComparableResponseViewItem vm)
            {
                double newWidth = vm.Width + e.HorizontalChange;
                const double min = 100;
                vm.Width = Math.Max(min, newWidth);
            }
        }
    }
}

public class ComparableResponseViewItem : BaseViewModel
{
    public double Width
    {
        get;
        set
        {
            if (Math.Abs(value - field) < double.Epsilon) return;
            field = value;
            OnPropertyChanged();
        }
    } = 650d;

    public FlowDocument? FullDocument
    {
        get { return GetAsyncProperty(() => RawItem.CreateFullResponseDocumentAsync()); }
    }

    public ClientResponseViewItem RawItem { get; }

    public ComparableResponseViewItem(ClientResponseViewItem responseViewItem)
    {
        RawItem = responseViewItem;
    }
}