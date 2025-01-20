using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient;

public class ModelSelectionViewModel : BaseViewModel
{
    private string? _selectedModelId;
    private ILLMEndpoint? _selectedEndpoint;

    public IList<ILLMEndpoint> AvailableEndpoints { get; set; } = new List<ILLMEndpoint>();

    public string? SelectedModelId
    {
        get => _selectedModelId;
        set
        {
            if (value == _selectedModelId) return;
            _selectedModelId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CreateModelCommand));
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
            OnPropertyChanged(nameof(CreateModelCommand));
        }
    }

    public ICommand CreateModelCommand => new ActionCommand((o =>
    {
        if (SelectedModelId == null || SelectedEndpoint == null)
        {
            MessageBox.Show("Please select model and endpoint");
            return;
        }

        var model = SelectedEndpoint.GetModel(SelectedModelId);
        if (model == null)
        {
            MessageBox.Show("create model failed!");
            return;
        }

  
        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    }));
}