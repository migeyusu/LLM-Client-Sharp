using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LLMClient.Component.Utility;

public class SuspendableObservableCollection<T> : ObservableCollection<T>, ISupportInitialize
{
    private bool _isSuspended;
    private int _suspendCount;

    public SuspendableObservableCollection() : base()
    {
    }

    public SuspendableObservableCollection(IEnumerable<T> enumerable) : base(enumerable)
    {
    }

    public void BeginInit()
    {
        _suspendCount++;
        _isSuspended = true;
    }

    /// <summary>
    /// same as endreset
    /// </summary>
    public void EndInit()
    {
        if (--_suspendCount == 0)
        {
            _isSuspended = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void EndAdd(IList<T> addItems)
    {
        if (--_suspendCount == 0)
        {
            _isSuspended = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)addItems));
        }
    }

    public void AddRange(IList<T> items)
    {
        BeginInit();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        EndInit();
    }

    public void ResetWith(IEnumerable<T> items)
    {
        BeginInit();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        EndInit();
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