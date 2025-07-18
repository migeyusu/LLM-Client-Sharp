using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LLMClient.UI.Component;

public class SuspendableObservableCollection<T> : ObservableCollection<T>, ISupportInitialize
{
    private bool _isSuspended;
    private int _suspendCount;

    public void BeginInit()
    {
        _suspendCount++;
        _isSuspended = true;
    }

    public void EndInit()
    {
        if (--_suspendCount == 0)
        {
            _isSuspended = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_isSuspended)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_isSuspended)
            base.OnPropertyChanged(e);
    }
}