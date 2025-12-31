using System.Windows.Input;
using LLMClient.Component.ViewModel;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    public ModelSelectionPopupViewModel(Action<BaseModelSelectionViewModel> successAction)
        : base(successAction)
    {
    }

    public RoutedCommand? SuccessRoutedCommand { get; set; } = DialogHost.CloseDialogCommand;

    protected override void SelectModel(BaseModelSelectionViewModel client)
    {
        SuccessAction?.Invoke(client);
        SuccessRoutedCommand?.Execute(true, null);
    }
}