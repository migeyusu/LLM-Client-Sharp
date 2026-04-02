using System.Collections.ObjectModel;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.UserControls;

namespace LLMClient.Endpoints;

/// <summary>
/// ViewModel for the Suggestion configuration dialog.
/// Delegates to <see cref="EndpointConfigureViewModel"/>.
/// </summary>
public class SuggestionViewModel
{
    private readonly EndpointConfigureViewModel _inner;

    public SuggestionViewModel(EndpointConfigureViewModel inner)
    {
        _inner = inner;
    }

    public IReadOnlyList<IEndpointModel> SuggestedModels => _inner.SuggestedModels;

    public ObservableCollection<IEndpointModel> SuggestedModelsOb => _inner.SuggestedModelsOb;

    public ModelSelectionPopupViewModel PopupSelectViewModel => _inner.PopupSelectViewModel;

    public ICommand RemoveSuggestedModelCommand => _inner.RemoveSuggestedModelCommand;

    public ICommand ReloadCommand => _inner.ReloadCommand;

    public ICommand SaveAllCommand => _inner.SaveAllCommand;
}

