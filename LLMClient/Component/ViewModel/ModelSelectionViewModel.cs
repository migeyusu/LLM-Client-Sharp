using LLMClient.Abstraction;

namespace LLMClient.Component.ViewModel;

public class ModelSelectionViewModel : BaseModelSelectionViewModel
{
    public Action<ILLMChatClient>? SuccessAction { get; }

    public ModelSelectionViewModel(Action<ILLMChatClient>? successAction = null)
    {
        SuccessAction = successAction;
    }

    protected override void SubmitClient(ILLMChatClient client)
    {
        SuccessAction?.Invoke(client);
    }
}