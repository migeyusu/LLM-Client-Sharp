using LLMClient.Dialog.Models;

namespace LLMClient.Dialog;

public class DialogItemEqualityComparer : IEqualityComparer<IDialogItem>
{
    public static DialogItemEqualityComparer Instance { get; } = new DialogItemEqualityComparer();

    public bool Equals(IDialogItem? x, IDialogItem? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        if (x is IInteractionItem interactionItem)
        {
            return interactionItem.InteractionId == ((IInteractionItem)y).InteractionId;
        }

        return x.GetHashCode() == y.GetHashCode();
    }

    public int GetHashCode(IDialogItem obj)
    {
        return obj.GetHashCode();
    }
}