using System.Collections.ObjectModel;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public abstract class BaseDialogItem : BaseViewModel, IDialogItem
{
    public abstract long Tokens { get; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public abstract DialogRole Role { get; }

    public IDialogItem? PreviousItem
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public abstract IDialogSession? Session { get; }

    public bool HasFork
    {
        get { return ChildItemsObservables.Count > 1; }
    }

    private ObservableCollection<IDialogItem> ChildItemsObservables { get; } = [];

    public IReadOnlyCollection<IDialogItem> Children => _childrenReadOnly;

    public int SiblingIndex => ((BaseDialogItem?)PreviousItem)?.ChildItemsObservables.IndexOf(this) ?? 0;

    public abstract bool IsAvailableInContext { get; }

    private readonly ReadOnlyObservableCollection<IDialogItem> _childrenReadOnly;

    protected BaseDialogItem()
    {
        _childrenReadOnly = new ReadOnlyObservableCollection<IDialogItem>(ChildItemsObservables);
    }

    public abstract IEnumerable<ChatMessage> Messages { get; }

    public IDialogItem AppendChild(IDialogItem child)
    {
        ((BaseDialogItem)child).PreviousItem = this;
        ChildItemsObservables.Add(child);
        OnPropertyChanged(nameof(HasFork));
        return child;
    }

    public IDialogItem RemoveChild(IDialogItem child)
    {
        if (ChildItemsObservables.Remove(child))
        {
            ((BaseDialogItem)child).PreviousItem = null;
            OnPropertyChanged(nameof(HasFork));
        }

        return child;
    }

    public void ClearChildren()
    {
        ChildItemsObservables.Clear();
        OnPropertyChanged(nameof(HasFork));
    }
}