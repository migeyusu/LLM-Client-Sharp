using LLMClient.Component.ViewModel;

namespace LLMClient.Dialog.Models;

public interface ISearchableDialogItem : IDialogItem
{
    SearchableDocument? SearchableDocument { get; }
}