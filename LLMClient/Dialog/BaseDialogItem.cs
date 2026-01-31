using System.Collections.ObjectModel;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class BaseDialogItem : BaseViewModel, IDialogItem
{
    public abstract long Tokens { get; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public abstract ChatRole Role { get; }

    private IDialogItem? _previousItem;

    public IDialogItem? PreviousItem
    {
        get => _previousItem;
        set
        {
            if (Equals(value, _previousItem)) return;
            _previousItem = value;
            OnPropertyChanged();
        }
    }

    public abstract string DisplayText { get; }
    
    private ObservableCollection<IDialogItem> ChildItemsObservables { get; } = [];

    public IReadOnlyCollection<IDialogItem> Children => _childrenReadOnly;

    public int SiblingIndex => ((BaseDialogItem?)PreviousItem)?.ChildItemsObservables.IndexOf(this) ?? 0;

    public abstract bool IsAvailableInContext { get; }

    private readonly ReadOnlyObservableCollection<IDialogItem> _childrenReadOnly;

    protected BaseDialogItem()
    {
        _childrenReadOnly = new ReadOnlyObservableCollection<IDialogItem>(ChildItemsObservables);
    }

    public abstract IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);

    public IDialogItem AppendChild(IDialogItem child)
    {
        ((BaseDialogItem)child).PreviousItem = this;
        ChildItemsObservables.Add(child);
        return child;
    }

    public IDialogItem RemoveChild(IDialogItem child)
    {
        if (ChildItemsObservables.Remove(child))
        {
            ((BaseDialogItem)child).PreviousItem = null;
        }

        return child;
    }

    public void ClearChildren()
    {
        ChildItemsObservables.Clear();
    }
}