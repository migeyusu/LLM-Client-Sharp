using System.Collections.ObjectModel;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class BaseDialogItem : BaseViewModel, IDialogItem
{
    public abstract long Tokens { get; }

    public Guid Id { get; set; } = Guid.NewGuid();
    
    private IDialogItem? _previousItem;
    public IDialogItem? PreviousItem
    {
        get => _previousItem;
        set
        {
            if (Equals(value, _previousItem)) return;
            _previousItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviousItemId));
        }
    }

    public Guid? PreviousItemId => PreviousItem?.Id;
    public ObservableCollection<IDialogItem> ChildItemsObservables { get; } = [];

    public IReadOnlyCollection<IDialogItem> Children => _childrenReadOnly;

    public abstract bool IsAvailableInContext { get; }

    private readonly ReadOnlyObservableCollection<IDialogItem> _childrenReadOnly;

    protected BaseDialogItem()
    {
        _childrenReadOnly = new ReadOnlyObservableCollection<IDialogItem>(ChildItemsObservables);
    }

    public abstract IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);

    public void AppendChild(IDialogItem child)
    {
        ((BaseDialogItem)child).PreviousItem = this;
        ChildItemsObservables.Add(child);
    }
}