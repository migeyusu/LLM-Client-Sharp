using System.Windows.Input;
using LLMClient.Component.ViewModel;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    private static readonly Lazy<ModelSelectionPopupViewModel> SharedLazy =
        new Lazy<ModelSelectionPopupViewModel>(() => new ModelSelectionPopupViewModel());

    public static ModelSelectionPopupViewModel Shared
    {
        get { return SharedLazy.Value; }
    }

    public ModelSelectionPopupViewModel(Action<BaseModelSelectionViewModel>? successAction = null)
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