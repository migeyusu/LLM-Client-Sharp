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
        if (x is RequestViewItem requestViewItem)
        {
            return requestViewItem.InteractionId == ((RequestViewItem)y).InteractionId;
        }

        if (x is MultiResponseViewItem multiResponseViewItem)
        {
            return multiResponseViewItem.InteractionId == ((MultiResponseViewItem)y).InteractionId;
        }

        return x.GetHashCode() == y.GetHashCode();
    }

    public int GetHashCode(IDialogItem obj)
    {
        return obj.GetHashCode();
    }
}