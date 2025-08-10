using LLMClient.Abstraction;
using LLMClient.UI.Component;

namespace LLMClient.Dialog;

public interface IResponseViewItem : IResponse, IDialogItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }

    string EndPointName { get; }
}