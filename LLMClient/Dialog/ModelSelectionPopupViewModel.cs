using System.Windows.Input;
using LLMClient.Component.ViewModel;
using MaterialDesignThemes.Wpf;

namespace LLMClient.Dialog;

public class ModelSelectionPopupViewModel : ModelSelectionViewModel
{
    private static readonly Lazy<ModelSelectionPopupViewModel> PopupSharedLazy =
        new(() => new ModelSelectionPopupViewModel()
        {
            SuccessRoutedCommand = PopupBox.ClosePopupCommand,
        });

    public static ModelSelectionPopupViewModel PopupShared
    {
        get { return PopupSharedLazy.Value; }
    }

    public ModelSelectionPopupViewModel(Action<BaseModelSelectionViewModel>? successAction = null)
        : base(successAction)
    {
    }

    public RoutedCommand? SuccessRoutedCommand { get; set; } = DialogHost.CloseDialogCommand;

    protected override void ApplyModel()
    {
        SuccessAction?.Invoke(this);
        SuccessRoutedCommand?.Execute(true, null);
    }
    
}