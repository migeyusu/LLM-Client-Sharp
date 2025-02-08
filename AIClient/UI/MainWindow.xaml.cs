// #define TESTMODE

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

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

    private async void CommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel dialogViewModel)
        {
            if ((await DialogHost.Show(new ConfirmView() { Header = "删除该会话吗？" })) is true)
            {
                _mainViewModel.DeleteDialog(dialogViewModel);
            }
        }
    }

    private bool _savingEnsured = false;

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_savingEnsured;
        _mainViewModel.QuitCommand.Execute(this);
        _savingEnsured = true;
    }
}