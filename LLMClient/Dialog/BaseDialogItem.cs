using System.Collections.ObjectModel;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class BaseDialogItem : BaseViewModel, IDialogItem
{
    public abstract long Tokens { get; }

    public Guid Id { get; } = Guid.NewGuid();

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

    public Guid? PreviousItemId => PreviousItem?.Id;

    public ObservableCollection<IDialogItem> ChildItemsObservables { get; } = [];

    public IReadOnlyCollection<IDialogItem> Children => _childrenReadOnly;

    public abstract bool IsAvailableInContext { get; }

    private readonly ReadOnlyObservableCollection<IDialogItem> _childrenReadOnly;
    private IDialogItem? _previousItem;

    protected BaseDialogItem()
    {
        _childrenReadOnly = new ReadOnlyObservableCollection<IDialogItem>(ChildItemsObservables);
    }

    public abstract IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken);
}