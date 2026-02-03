using LLMClient.Abstraction;

namespace LLMClient.Component.ViewModel;

public class ModelSelectionViewModel : BaseModelSelectionViewModel
{
    public Action<BaseModelSelectionViewModel>? SuccessAction { get; set; }

    public ModelSelectionViewModel(Action<BaseModelSelectionViewModel>? successAction = null)
    {
        SuccessAction = successAction;
    }

    protected override void SelectModel(BaseModelSelectionViewModel client)
    {
        SuccessAction?.Invoke(client);
    }
}