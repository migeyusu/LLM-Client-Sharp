// #define TESTMODE

using System.ComponentModel;
using System.Windows;

namespace LLMClient.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    MainViewModel _mainViewModel;

    public MainWindow(MainViewModel mainViewModel)
    {
        this._mainViewModel = mainViewModel;
        this.DataContext = mainViewModel;
        InitializeComponent();
    }

    private bool _savingEnsured = false;

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_savingEnsured;
        _mainViewModel.QuitCommand.Execute(this);
        _savingEnsured = true;
    }
}