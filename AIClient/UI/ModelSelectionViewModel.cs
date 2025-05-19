using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class ModelSelectionViewModel : BaseViewModel
{
    public IEnumerable<ILLMEndpoint> AvailableEndpoints
    {
        get => _availableEndpoints;
        set
        {
            if (Equals(value, _availableEndpoints)) return;
            _availableEndpoints = value;
            OnPropertyChanged();
        }
    }

    public ModelSelectionViewModel()
    {
        _availableEndpoints = Array.Empty<ILLMEndpoint>();
    }

    public string? SelectedModelName
    {
        get => _selectedModelName;
        set
        {
            if (value == _selectedModelName) return;
            _selectedModelName = value;
            OnPropertyChanged();
        }
    }

    public ILLMEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set
        {
            if (Equals(value, _selectedEndpoint)) return;
            _selectedEndpoint = value;
            OnPropertyChanged();
        }
    }


    private string? _selectedModelName;

    private ILLMEndpoint? _selectedEndpoint;
    private IEnumerable<ILLMEndpoint> _availableEndpoints;

    public ModelSelectionViewModel(IEnumerable<ILLMEndpoint> availableEndpoints)
    {
        _availableEndpoints = availableEndpoints;
    }

    public ILLMModelClient? GetClient()
    {
        if (this.SelectedModelName == null)
        {
            return null;
        }

        var model = this.SelectedEndpoint?.NewClient(this.SelectedModelName);
        return model;
    }
}