// #define TESTMODE

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using TextMateSharp.Grammars;

namespace LLMClient.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : ExtendedWindow
{
    MainViewModel _mainViewModel;

    GlobalConfig _globalConfig;

    public MainWindow(MainViewModel mainViewModel, GlobalConfig globalConfig)
    {
        this._mainViewModel = mainViewModel;
        this._globalConfig = globalConfig;
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
    

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        
    }

    private async void OpenConfig_OnClick(object sender, RoutedEventArgs e)
    {
        await this.ShowDialog(_globalConfig);
    }
}