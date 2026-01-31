using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Dialog;

public class NavigationViewModel : BaseViewModel
{
    public NavigationViewModel(INavigationViewModel model)
    {
        Model = model;
    }

    public INavigationViewModel Model { get; set; }
}