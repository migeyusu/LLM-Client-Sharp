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
using LLMClient.Project;
using LLMClient.ToolCall;
using MaterialDesignThemes.Wpf;
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
            if (Debugger.IsAttached)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (IsWindowNotInForeground())
                {
                    await mainWindowViewModel.SaveDataAsync();
                }
            });
        });
        //间隔一分钟检查保存
        _timer.Change(60000, 60000);
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fileEditRequestViewModel = new FileEditRequestViewModel
        {
            OriginalContent = """
                              agent:
                                system_template: |
                                  You are a helpful assistant that can interact with a computer.
                                instance_template: |
                                  Please solve this issue: {{task}}
                              
                                  You can execute bash commands and edit files to implement the necessary changes.
                              
                                  ## Recommended Workflow
                              
                                  This workflow should be done step-by-step so that you can iterate on your changes and any possible problems.
                              
                                  1. Analyze the codebase by finding and reading relevant files
                                  2. Create a script to reproduce the issue
                                  3. Edit the source code to resolve the issue
                                  4. Verify your fix works by running your script again
                                  5. Test edge cases to ensure your fix is robust
                                  6. Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                                     Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>
                              
                                  ## Command Execution Rules
                              
                                  You are operating in an environment where
                              
                                  1. You issue at least one command
                                  2. The system executes the command(s) in a subshell
                                  3. You see the result(s)
                                  4. You write your next command(s)
                              
                                  Each response should include:
                              
                                  1. **Reasoning text** where you explain your analysis and plan
                                  2. At least one tool call with your command
                              
                                  **CRITICAL REQUIREMENTS:**
                              
                                  - Your response SHOULD include reasoning text explaining what you're doing
                                  - Your response MUST include AT LEAST ONE bash tool call
                                  - Directory or environment variable changes are not persistent. Every action is executed in a new subshell.
                                  - However, you can prefix any action with `MY_ENV_VAR=MY_VALUE cd /path/to/working/dir && ...` or write/load environment variables from files
                                  - Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                                    Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>
                              
                                  Example of a CORRECT response:
                                  <example_response>
                                  I need to understand the structure of the repository first. Let me check what files are in the current directory to get a better understanding of the codebase.
                              
                                  [Makes bash tool call with {"command": "ls -la"} as arguments]
                                  </example_response>
                              
                                  <system_information>
                                  {{system}} {{release}} {{version}} {{machine}}
                                  </system_information>
                                  
                                  {{{project_information}}}
                              
                                  ## Useful command examples
                              
                                  ### Create a new file:
                              
                                  ```bash
                                  cat <<'EOF' > newfile.py
                                  import numpy as np
                                  hello = "world"
                                  print(hello)
                                  EOF
                                  ```
                              
                                  ### Edit files with sed:
                              
                                  {%- if system == "Darwin" -%}
                                  <important>
                                  You are on MacOS. For all the below examples, you need to use `sed -i ''` instead of `sed -i`.
                                  </important>
                                  {%- endif -%}
                              
                                  ```bash
                                  # Replace all occurrences
                                  sed -i 's/old_string/new_string/g' filename.py
                              
                                  # Replace only first occurrence
                                  sed -i 's/old_string/new_string/' filename.py
                              
                                  # Replace first occurrence on line 1
                                  sed -i '1s/old_string/new_string/' filename.py
                              
                                  # Replace all occurrences in lines 1-10
                                  sed -i '1,10s/old_string/new_string/g' filename.py
                                  ```
                              
                                  ### View file content:
                              
                                  ```bash
                                  # View specific lines with numbers
                                  nl -ba filename.py | sed -n '10,20p'
                                  ```
                              
                                  ### Any other command you want to run
                              
                                  ```bash
                                  anything
                                  ```
                                step_limit: 0
                                cost_limit: 3.
                                mode: confirm
                              environment:
                                env:
                                  PAGER: cat
                                  MANPAGER: cat
                                  LESS: -R
                                  PIP_PROGRESS_BAR: 'off'
                                  TQDM_DISABLE: '1'
                              model:
                                observation_template: |
                                  {%- if output.output | length < 10000 -%}
                                  {
                                    "returncode": {{ output.returncode }},
                                    "output": {{ output.output | tojson }}
                                    {%- if output.exception_info %}, "exception_info": {{ output.exception_info | tojson }}{% endif %}
                                  }
                                  {%- else -%}
                                  {
                                    "returncode": {{ output.returncode }},
                                    "output_head": {{ output.output[:5000] | tojson }},
                                    "output_tail": {{ output.output[-5000:] | tojson }},
                                    "elided_chars": {{ output.output | length - 10000 }},
                                    "warning": "Output too long."
                                    {%- if output.exception_info %}, "exception_info": {{ output.exception_info | tojson }}{% endif %}
                                  }
                                  {%- endif -%}
                                format_error_template: |
                                  Tool call error:
                              
                                  <error>
                                  {{error}}
                                  </error>
                              
                                  Here is general guidance on how to submit correct toolcalls:
                              
                                  Every response needs to use the 'bash' tool at least once to execute commands.
                              
                                  Call the bash tool with your command as the argument:
                                  - Tool: bash
                                  - Arguments: {"command": "your_command_here"}
                              
                                  If you want to end the task, please issue the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`
                                  without any other command.
                                model_kwargs:
                                  drop_params: true
                              
                              """,
            UpdatedContent = """
                             agent:
                               system_template: |
                                 You are a helpful assistant that can interact with a computer.
                               instance_template: |
                                 Please solve this issue: {{task}}
                             
                                 You can execute bash commands and edit files to implement the necessary changes.
                             
                                 ## Recommended Workflow
                             
                                 This workflow should be done step-by-step so that you can iterate on your changes and any possible problems.
                             
                                 1. Analyze the codebase by finding and reading relevant files
                                 2. Create a script to reproduce the issue
                                 3. Edit the source code to resolve the issue
                                 4. Verify your fix works by running your script again
                                 5. Test edge cases to ensure your fix is robust
                                 6. Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                                    Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>
                             
                                 ## Command Execution Rules
                             
                                 You are operating in an environment where
                             
                                 1. You issue at least one command
                                 2. The system executes the command(s) in a subshell
                                 3. You see the result(s)
                                 4. You write your next command(s)
                             
                                 Each response should include:
                             
                                 1. **Reasoning text** where you explain your analysis and plan
                                 2. At least one tool call with your command
                             
                                 **CRITICAL REQUIREMENTS:**
                             
                                 - Your response SHOULD include reasoning text explaining what you're doing
                                 - Your response MUST include AT LEAST ONE bash tool call
                                 - Directory or environment variable changes are not persistent. Every action is executed in a new subshell.
                                 - However, you can prefix any action with `MY_ENV_VAR=MY_VALUE cd /path/to/working/dir && ...` or write/load environment variables from files
                                 - Submit your changes and finish your work by issuing the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`.
                                   Do not combine it with any other command. <important>After this command, you cannot continue working on this task.</important>
                             
                                 Example of a CORRECT response:
                                 <example_response>
                                 I need to understand the structure of the repository first. Let me check what files are in the current directory to get a better understanding of the codebase.
                             
                                 [Makes bash tool call with {"command": "ls -la"} as arguments]
                                 </example_response>
                             
                                 <system_information>
                                 {{system}} {{release}} {{version}} {{machine}}
                                 </system_information>
                                 
                                 {{{project_information}}}
                             
                                 ## Useful command examples
                             
                                 ### Create a new file:
                             
                                 ```bash
                                 cat <<'EOF' > newfile.py
                                 import numpy as np
                                 hello = "world"
                                 print(hello)
                                 EOF
                                 ```
                             
                                 ### Edit files with sed:
                             
                                 {%- if system == "Darwin" -%}
                                 <important>
                                 You are on MacOS. For all the below examples, you need to use `sed -i ''` instead of `sed -i`.
                                 </important>
                                 {%- endif -%}
                             
                                 ```bash
                                 # Replace all occurrences
                                 sed -i 's/old_string/new_string/g' filename.py
                             
                                 # Replace only first occurrence
                                 sed -i 's/old_string/new_string/' filename.py
                             
                                 # Replace first occurrence on line 1
                                 sed -i '1s/old_string/new_string/' filename.py
                             
                                 # Replace all occurrences in lines 1-10
                                 sed -i '1,10s/old_string/new_string/g' filename.py
                                 ```
                             
                                 ### View file content:
                             
                                 ```bash
                                 # View specific lines with numbers
                                 nl -ba filename.py | sed -n '10,20p'
                                 ```
                             
                                 ### Any other command you want to run
                             
                                 ```bash
                                 anything
                                 ```
                               step_limit: 0
                               cost_limit: 3.
                               mode: confirm
                             environment:
                               env:
                                 PAGER: cat
                                 MANPAGER: cat
                                 LESS: -R
                                 PIP_PROGRESS_BAR: 'off'
                                 TQDM_DISABLE: '1'
                             model:
                               observation_template: |
                                 {%- if output.output | length < 10000 -%}
                                 {
                                   "returncode": {{ output.returncode }},
                                   "output": {{ output.output | tojson }}
                                   {%- if output.exception_info %}, "exception_info": {{ output.exception_info | tojson }}{% endif %}
                                 }
                                 {%- else -%}
                                 {
                                   "returncode": {{ output.returncode }},
                                   "output_head": {{ output.output[:5000] | tojson }},
                                   "output_tail": {{ output.output[-5000:] | tojson }},
                                   "elided_chars": {{ output.output | length - 10000 }},
                                   "warning": "Output too long."
                                   {%- if output.exception_info %}, "exception_info": {{ output.exception_info | tojson }}{% endif %}
                                 }
                                 {%- endif -%}
                               format_error_template: |
                                 Tool call error:
                             
                                 <error>
                                 {{error}}
                                 </error>
                             
                                 Here is general guidance on how to submit correct toolcalls:
                             
                                 Every response needs to use the 'bash' tool at least once to execute commands.
                             
                                 Call the bash tool with your command as the argument:
                                 - Tool: bash
                                 - Arguments: {"command": "your_command_here"}
                             
                                 If you want to end the task, please issue the following command: `echo COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT`
                                 without any other command.
                               
                                 drop_params: true
                             
                             """,
            Title = $"Apply changes to sadfasdas",
            Description = $"Review the proposed file changes before applying them.",
        };
        await DialogHost.Show(fileEditRequestViewModel);
        
    }

    private async void Delete_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is FileBasedSessionBase session)
        {
            if (session.IsBusy)
            {
                _mainWindowViewModel.MessageQueue.Enqueue("请停止当前响应后再删除会话");
                return;
            }

            if (await Extension.ShowConfirm("删除该会话吗？"))
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
        if (e.Parameter is IEndpointModel suggested)
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

    private void CloneCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            var session = _mainWindowViewModel.PreSession;
            if (session != null)
            {
                var clone = session.Clone();
                if (clone is FileBasedSessionBase sessionClone)
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

    private async void SaveAs_Command_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            var session = _mainWindowViewModel.PreSession;
            if (session != null)
            {
                await session.SaveAs();
                MessageEventBus.Publish("已保存会话");
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish("保存会话失败: " + exception.Message);
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
        DialogHost.OpenDialogCommand.Execute(statsViewModel, sender as IInputElement);
    }

    private void OpenArchiveManager_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var archiveManager = new ArchiveManagerViewModel();
            archiveManager.RestoreCompleted += (_, filePath) => _ = _mainWindowViewModel.ReloadSession(filePath);
            DialogHost.OpenDialogCommand.Execute(archiveManager, sender as IInputElement);
        }
        catch (Exception exception)
        {
            _mainWindowViewModel.MessageQueue.Enqueue("无法打开归档管理: " + exception.Message);
        }
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

    private const string BackupFolderName = "Archive";
    private const string DialogArchive = "DialogArchive";
    private const string ProjectArchive = "ProjectArchive";

    private async void Backup_CommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        //自动备份到archive文件夹
        try
        {
            if (e.Parameter is FileBasedSessionBase sessionBase)
            {
                if (sessionBase.IsBusy)
                {
                    MessageBoxes.Warning("请停止当前响应后再备份会话");
                    return;
                }

                var fullPath = Path.GetFullPath(BackupFolderName);
                string specificArchive = "";
                if (sessionBase is DialogFileViewModel)
                {
                    specificArchive = DialogArchive;
                    fullPath = Path.Combine(fullPath, specificArchive);
                }
                else if (sessionBase is ProjectViewModel)
                {
                    specificArchive = ProjectArchive;
                    fullPath = Path.Combine(fullPath, specificArchive);
                }

                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }

                var fileName = Path.GetFileNameWithoutExtension(sessionBase.FileFullPath);
                var extension = Path.GetExtension(sessionBase.FileFullPath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{fileName}_{timestamp}{extension}";

                var path = Path.Combine(fullPath, backupFileName);
                await sessionBase.SaveAs(path);
                MessageEventBus.Publish($"已备份");
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            MessageEventBus.Publish("备份会话失败: " + exception.Message);
        }
    }

    private void CloneTemplate_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            var session = _mainWindowViewModel.PreSession;
            if (session != null)
            {
                var clone = session.CloneHeader();
                if (clone is FileBasedSessionBase sessionClone)
                {
                    _mainWindowViewModel.AddSession(sessionClone);
                    _mainWindowViewModel.MessageQueue.Enqueue("克隆模板会话成功");
                }
                else
                {
                    _mainWindowViewModel.MessageQueue.Enqueue("克隆模板会话失败: 无法克隆该类型的会话");
                }
            }
        }
        catch (Exception exception)
        {
            MessageEventBus.Publish("克隆模板会话失败: " + exception.Message);
        }
    }

    private async void ImportDialogSession_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (_mainWindowViewModel.PreSession is ProjectViewModel projectViewModel)
        {
            var availableDialogs = _mainWindowViewModel.SessionViewModels
                .OfType<DialogFileViewModel>()
                .Select(d => d.Dialog)
                .ToList();
            await projectViewModel.ImportFromDialogSessions(availableDialogs);
        }
    }
}