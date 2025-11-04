// #define TESTMODE

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Configuration;
using LLMClient.Dialog;
using LLMClient.UI.Component.CustomControl;
using LLMClient.UI.Component.Utility;
using LLMClient.UI.Log;
using LLMClient.UI.Render;
using LLMClient.UI.ViewModel;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : ExtendedWindow
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly GlobalOptions _globalConfig;

    public MainWindow(MainWindowViewModel mainWindowViewModel, GlobalOptions globalConfig)
    {
        this._mainWindowViewModel = mainWindowViewModel;
        _globalConfig = globalConfig;
        this.DataContext = mainWindowViewModel;
        InitializeComponent();
    }

    private async void Delete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ILLMSession session)
        {
            if (session.IsBusy)
            {
                _mainWindowViewModel.MessageQueue.Enqueue("请停止当前响应后再删除会话");
                return;
            }

            if ((await DialogHost.Show(new ConfirmView() { Header = "删除该会话吗？" })) is true)
            {
                _mainWindowViewModel.DeleteSession(session);
            }
        }
    }

    private bool _closing;

    private bool _closeRequest;

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = !_closeRequest;
        if (_closeRequest)
        {
            return;
        }

        if (_mainWindowViewModel.IsBusy)
        {
            _mainWindowViewModel.MessageQueue.Enqueue("请等待当前响应完成后再关闭窗口");
            return;
        }

        if (_mainWindowViewModel.RagSourceCollection.IsRunning)
        {
            _mainWindowViewModel.MessageQueue.Enqueue("请等待Rag完成后再关闭窗口");
            return;
        }

        if (!_closing)
        {
            Task.Run((async () =>
            {
                _closing = true;
                try
                {
                    await _mainWindowViewModel.SaveSessions();
                }
                catch (Exception exception)
                {
                    MessageBox.Show("保存会话数据失败: " + exception.Message);
                }

                _closing = false;
                _closeRequest = true;
                Dispatcher.Invoke(this.Close);
            }));
        }
    }

    private async void OpenConfig_OnClick(object sender, RoutedEventArgs e)
    {
        await this.ShowDialog(_globalConfig);
    }

    #region dialog

    private void SnapNewDialog_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ILLMChatModel suggested)
        {
            var llmModelClient = suggested.CreateChatClient();
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

    private void DialogBranch_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is IDialogItem dialogViewItem)
        {
            _mainWindowViewModel.ForkPreDialog(dialogViewItem);
        }
    }

    private async void DialogReprocess_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is DialogViewModel oldDialog)
        {
            var selectionViewModel = new ModelSelectionPopupViewModel((client =>
            {
                var newDialog = _mainWindowViewModel.AddNewDialog(client, oldDialog.Topic);
                newDialog.Dialog.SequentialChain(oldDialog.DialogItems);
            }));
            await DialogHost.Show(selectionViewModel);
        }
    }

    private void CloneCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            var session = _mainWindowViewModel.PreSession;
            if (session != null)
            {
                var clone = session.Clone();
                if (clone is ILLMSession sessionClone)
                {
                    _mainWindowViewModel.AddSession(sessionClone);
                    _mainWindowViewModel.MessageQueue.Enqueue("克隆会话成功");
                }
                else
                {
                    _mainWindowViewModel.MessageQueue.Enqueue("克隆会话失败: 无法克隆该类型的会话");
                }
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish("克隆会话失败: " + exception.Message);
        }
    }

    #endregion

    private readonly LoggerWindow _logWindow = new LoggerWindow();

    private void OpenLogger_OnClick(object sender, RoutedEventArgs e)
    {
        _logWindow.Show();
        _logWindow.Activate();
    }

    protected override void OnClosed(EventArgs e)
    {
        this._logWindow.Shutdown();
        MathJaxLatexRenderService.DisposeInstance();
        base.OnClosed(e);
    }

    private async void BackupCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            var session = _mainWindowViewModel.PreSession;
            if (session != null)
            {
                await session.Backup();
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish("备份会话失败: " + exception.Message);
        }
    }

    private void OpenSessionFile_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_mainWindowViewModel.PreSession is FileBasedSessionBase fileBasedSession)
        {
            Process.Start("explorer.exe", $"/select,\"{fileBasedSession.FileFullPath}\"");
        }
    }

    /*private void LeftPanelCollapse_OnClick(object sender, RoutedEventArgs e)
    {
        LeftPanelColumnDefinition.MinWidth
    }*/
}