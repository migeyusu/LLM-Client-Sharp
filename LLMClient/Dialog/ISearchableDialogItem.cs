using LLMClient.Component.ViewModel;

namespace LLMClient.Dialog;

public interface ISearchableDialogItem : IDialogItem
{
    SearchableDocument? SearchableDocument { get; }
}