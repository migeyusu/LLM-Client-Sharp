using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;

namespace LLMClient.Dialog;

public interface IResponseViewItem : IResponse, IDialogItem
{
    ThemedIcon Icon { get; }

    string ModelName { get; }

    string EndPointName { get; }
}