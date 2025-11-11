using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.UI.ViewModel;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    public ModelSelectionPopupViewModel(Action<ILLMChatClient> successAction)
        : base(successAction)
    {
    }

    public RoutedCommand? SuccessRoutedCommand { get; set; } = DialogHost.CloseDialogCommand;

    protected override void SubmitClient(ILLMChatClient client)
    {
        SuccessAction?.Invoke(client);
        //todo:test
        SuccessRoutedCommand?.Execute(true, null);
    }
}