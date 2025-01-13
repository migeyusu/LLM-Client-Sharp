// #define TESTMODE

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using Azure;
using Azure.AI.Inference;
using Markdig;
using Markdig.Renderers;
using Markdig.Wpf;


namespace LLMClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        this.DataContext = this;
        InitializeComponent();
    }

    public DialogViewModel PreDialog { get; set; } = new DialogViewModel();
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual async void OnPropertyChangedAsync([CallerMemberName] string? propertyName = null)
    {
        if (Dispatcher.CheckAccess())
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            await Dispatcher.InvokeAsync(
                () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}