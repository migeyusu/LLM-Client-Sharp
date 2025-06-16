// #define TESTMODE

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
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

    private async void Delete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel dialogViewModel)
        {
            if (dialogViewModel.IsResponding)
            {
                _mainViewModel.MessageQueue.Enqueue("请等待当前响应完成后再删除会话");
                return;
            }

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

    private void SnapNewDialog_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is SuggestedModel suggested)
        {
            var llmModelClient = suggested.Endpoint.NewClient(suggested.LlmModel.Name);
            if (llmModelClient != null)
            {
                _mainViewModel.AddNewDialog(llmModelClient);
            }
            else
            {
                _mainViewModel.MessageQueue.Enqueue("创建模型失败，请检查配置");
            }
        }
    }

    private void Branch_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogViewItem dialogViewItem)
        {
            _mainViewModel.ForkPreDialog(dialogViewItem);
        }
    }

    private async void Reprocess_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel oldDialog)
        {
            var selectionViewModel = new DialogCreationViewModel(_mainViewModel.EndpointsViewModel);
            if (await DialogHost.Show(selectionViewModel) is true)
            {
                var model = selectionViewModel.GetClient();
                if (model == null)
                {
                    MessageBox.Show("No model created!");
                    return;
                }

                var newDialog = _mainViewModel.AddNewDialog(model, oldDialog.Topic);
                newDialog.Client.Parameters.SystemPrompt = oldDialog.Client.Parameters.SystemPrompt;
                newDialog.SequentialChain(oldDialog.DialogItems);
            }
        }
    }
}