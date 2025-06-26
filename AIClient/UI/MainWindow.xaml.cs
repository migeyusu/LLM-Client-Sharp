// #define TESTMODE

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.UI.Component;
using LLMClient.UI.Dialog;
using LLMClient.UI.Log;
using LLMClient.UI.MCP;
using MaterialDesignThemes.Wpf;
using TextMateSharp.Grammars;

namespace LLMClient.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : ExtendedWindow
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly GlobalConfig _globalConfig;

    public MainWindow(MainWindowViewModel mainWindowViewModel, GlobalConfig globalConfig)
    {
        this._mainWindowViewModel = mainWindowViewModel;
        this._globalConfig = globalConfig;
        this.DataContext = mainWindowViewModel;
        InitializeComponent();
    }

    private async void Delete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel dialogViewModel)
        {
            if (dialogViewModel.IsResponding)
            {
                _mainWindowViewModel.MessageQueue.Enqueue("请等待当前响应完成后再删除会话");
                return;
            }

            if ((await DialogHost.Show(new ConfirmView() { Header = "删除该会话吗？" })) is true)
            {
                _mainWindowViewModel.DeleteDialog(dialogViewModel);
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

    private void SnapNewDialog_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is SuggestedModel suggested)
        {
            var llmModelClient = suggested.Endpoint.NewClient(suggested.LlmModel.Name);
            if (llmModelClient != null)
            {
                _mainWindowViewModel.AddNewDialog(llmModelClient);
            }
            else
            {
                _mainWindowViewModel.MessageQueue.Enqueue("创建模型失败，请检查配置");
            }
        }
    }

    private void Branch_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            _mainWindowViewModel.ForkPreDialog(dialogViewItem);
        }
    }

    private async void Reprocess_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel oldDialog)
        {
            var selectionViewModel = new DialogCreationViewModel(_mainWindowViewModel.EndpointsViewModel);
            if (await DialogHost.Show(selectionViewModel) is true)
            {
                var model = selectionViewModel.GetClient();
                if (model == null)
                {
                    MessageBox.Show("No model created!");
                    return;
                }

                var newDialog = _mainWindowViewModel.AddNewDialog(model, oldDialog.Topic);
                newDialog.Client.Parameters.SystemPrompt = oldDialog.Client.Parameters.SystemPrompt;
                newDialog.SequentialChain(oldDialog.DialogItems);
            }
        }
    }


    private readonly LoggerWindow _logWindow = new LoggerWindow();

    private void OpenLogger_OnClick(object sender, RoutedEventArgs e)
    {
        _logWindow.Show();
        _logWindow.Activate();
    }

    protected override void OnClosed(EventArgs e)
    {
        this._logWindow.Shutdown();
        base.OnClosed(e);
    }
}