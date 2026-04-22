using System.Collections.ObjectModel;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public abstract class MultiResponseViewItem<T> : BaseDialogItem, IResponseItem
    where T : ResponseViewItemBase
{
    public Guid InteractionId
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public DialogSessionViewModel ParentSession { get; }

    public ObservableCollection<T> Items { get; }

    public abstract bool IsResponding { get; protected set; }

    public override DialogRole Role => DialogRole.Response;

    public MultiResponseViewItem(IEnumerable<T> items, DialogSessionViewModel parentSession)
    {
        ParentSession = parentSession;
        Items = new ObservableCollection<T>(items);
    }
}