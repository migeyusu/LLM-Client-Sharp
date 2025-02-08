using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.UI;

public class ModelSelectionViewModel : BaseViewModel
{
    private string? _selectedModelName;
    private ILLMEndpoint? _selectedEndpoint;
    private string _dialogName = "新建会话";

    public IList<ILLMEndpoint>? AvailableEndpoints { get; set; }

    public string DialogName
    {
        get => _dialogName;
        set
        {
            if (value == _dialogName) return;
            _dialogName = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedModelName
    {
        get => _selectedModelName;
        set
        {
            if (value == _selectedModelName) return;
            _selectedModelName = value;
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
        if (SelectedModelName == null || SelectedEndpoint == null)
        {
            MessageBox.Show("Please select model and endpoint");
            return;
        }

        var model = SelectedEndpoint.GetModel(SelectedModelName);
        if (model == null)
        {
            MessageBox.Show("create model failed!");
            return;
        }


        var frameworkElement = o as FrameworkElement;
        DialogHost.CloseDialogCommand.Execute(true, frameworkElement);
    }));
}