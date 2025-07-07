using LLMClient.Abstraction;
using LLMClient.UI.Component;

namespace LLMClient.UI.Dialog;

public interface IResponseViewItem : IResponse, IDialogItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }

    string EndPointName { get; }
}