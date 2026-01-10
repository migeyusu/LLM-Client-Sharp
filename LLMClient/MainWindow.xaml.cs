// #define TESTMODE

using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Dialog;
using MaterialDesignThemes.Wpf;
using ConfirmView = LLMClient.Component.UserControls.ConfirmView;
using LoggerWindow = LLMClient.Log.LoggerWindow;

namespace LLMClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : ExtendedWindow, IDisposable
{
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly Timer _timer;

    public MainWindow(MainWindowViewModel mainWindowViewModel)
    {
        this._mainWindowViewModel = mainWindowViewModel;
        this.DataContext = mainWindowViewModel;
        InitializeComponent();
        _timer = new Timer(state =>
        {
            //执行自动保存
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (IsWindowNotInForeground())
                {
                    await mainWindowViewModel.SaveDataAsync();
                }
            });
        });
        //间隔一分钟检查保存
        _timer.Change(0, 60000);
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
                    await _mainWindowViewModel.SaveDataAsync();
                }
                catch (Exception exception)
                {
                    Trace.TraceError("保存会话数据失败: " + exception.Message);
                }

                _closing = false;
                _closeRequest = true;
                Dispatcher.Invoke(this.Close);
            }));
        }
    }

    #region dialog

    private void SnapNewDialog_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ILLMModel suggested)
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
                var newDialog = _mainWindowViewModel.AddNewDialog(client.CreateClient(), oldDialog.Topic);
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

    private readonly LoggerWindow _logWindow = new();

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

    private void DialogListsSwitchButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggleButton)
        {
            if (toggleButton.IsChecked == true)
            {
                LeftColumnDefinition.Width = new GridLength(260);
                LeftColumnDefinition.MinWidth = 230;
                _mainWindowViewModel.IsLeftDrawerOpen = true;
            }
            else
            {
                LeftColumnDefinition.Width = new GridLength(0);
                LeftColumnDefinition.MinWidth = 0;
                _mainWindowViewModel.IsLeftDrawerOpen = false;
            }
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        Application.Current.MainWindow = this;
    }

    private void OpenStatistics_OnClick(object sender, RoutedEventArgs e)
    {
        var statsViewModel = new UsageStatisticsViewModel(_mainWindowViewModel.EndpointsViewModel);
        DialogHost.OpenDialogCommand.Execute(statsViewModel, null);
    }

    /// <summary>
    /// 判断窗口是否处于非前台状态（最小化或失去焦点）
    /// </summary>
    /// <returns>true表示非前台，false表示在前台</returns>
    private bool IsWindowNotInForeground()
    {
        // 窗口最小化 或者 窗口非激活状态
        return this.WindowState == WindowState.Minimized || !this.IsActive;
    }

    public void Dispose()
    {
        _mainWindowViewModel.Dispose();
        _timer.Dispose();
    }
}