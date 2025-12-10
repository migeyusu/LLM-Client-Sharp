using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using LLMClient.Component.UserControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Wpf;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.Component.Render;

public class CodeViewModel : BaseViewModel, CommonCommands.ICopyable
{
    public CodeViewModel(WpfRenderer renderer,
        StringLineGroup codeGroup, string? extension, string? name,
        IGrammar? grammar = null)
    {
        Extension = extension;
        Name = name ?? string.Empty;
        _nameLower = Name.ToLower().Trim();
        CodeGroup = codeGroup;
        Grammar = grammar;
        _codeStringLazy = new Lazy<string>(codeGroup.ToString);
        RenderCode(renderer, grammar, codeGroup);
    }

    public IGrammar? Grammar { get; }

    public string Name { get; }

    public string? Extension { get; }

    private readonly Lazy<string> _codeStringLazy;
    public string CodeString => _codeStringLazy.Value;

    public StringLineGroup CodeGroup { get; }

    private readonly string[] _supportedRunExtensions = new[] { "bash", "powershell", "html" };

    private readonly string _nameLower;

    public bool CanRun
    {
        get { return !string.IsNullOrEmpty(Name) && _supportedRunExtensions.Contains(_nameLower); }
    }

    public ICommand RunCommand => new ActionCommand(o =>
    {
        try
        {
            //可以通过webview执行html
            var s = CodeString;
            if (!string.IsNullOrEmpty(s))
            {
                if (_nameLower.Equals("html"))
                {
                    var webView2 = new WebView2()
                    {
                        Source = new Uri("about:blank"),
                    };
                    webView2.EnsureCoreWebView2Async().ConfigureAwait(true).GetAwaiter().OnCompleted((() =>
                    {
                        webView2.CoreWebView2.NavigateToString(s);
                    }));
                    var window = new Window()
                    {
                        Title = "HTML Preview",
                        Width = 800,
                        Height = 600,
                        Content = webView2
                    };
                    window.Show();
                }
                else if (_nameLower.Equals("bash") || _nameLower.Equals("powershell"))
                {
                    ExecuteScriptLogic(s, _nameLower);
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
    });

    public ICommand SaveCommand => new ActionCommand(o =>
    {
        var s = GetCopyText();
        if (!string.IsNullOrEmpty(s))
        {
            try
            {
                var saveFileDialog = new SaveFileDialog();
                if (!string.IsNullOrEmpty(Extension))
                {
                    var defaultExt = Extension.TrimStart('.');
                    saveFileDialog.Filter = $"Code files (*.{defaultExt})|*.{defaultExt}|All files (*.*)|*.*";
                    saveFileDialog.DefaultExt = defaultExt;
                }
                else
                {
                    saveFileDialog.Filter = "Code files (*.*)|*.*";
                    saveFileDialog.DefaultExt = "txt";
                }

                if (saveFileDialog.ShowDialog() == true)
                {
                    var fileName = saveFileDialog.FileName;
                    File.WriteAllText(fileName, s);
                    MessageEventBus.Publish($"Code saved to {fileName}");
                }
            }
            catch (Exception e)
            {
                MessageEventBus.Publish(e.Message);
            }
        }
    });

    private void ExecuteScriptLogic(string scriptContent, string type)
    {
        // --- 1. 环境选择与检查 ---
        BashEnvironment? selectedBashEnv = null;
        if (type.Equals("bash"))
        {
            var environments = BashEnvironmentDetector.GetAvailableEnvironments();
            if (environments.Count == 0)
            {
                MessageBox.Show("未找到任何可执行 Bash 的环境。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 只有CMD时，也直接用，或者强制弹窗让用户确认
            if (environments.Count == 1)
            {
                selectedBashEnv = environments[0];
            }
            else
            {
                // 假设您已经实现了 ExecutorSelectionWindow，代码同上一次回答
                var selectWin = new ExecutorSelectionWindow(environments);
                if (selectWin.ShowDialog() != true) return;
                selectedBashEnv = selectWin.SelectedEnvironment;
            }

            if (selectedBashEnv == null)
            {
                return;
            }
        }

        // --- 2. 保存文件 ---
        var openFolderDialog = new OpenFolderDialog()
        {
            Title = "选择脚本执行目录",
            ValidateNames = true
        };

        if (openFolderDialog.ShowDialog() == true && !string.IsNullOrEmpty(openFolderDialog.FolderName))
        {
            try
            {
                string folder = openFolderDialog.FolderName;
                string extension;
                string finalContent;
                if (type.Equals("powershell"))
                {
                    extension = ".ps1";
                    finalContent = scriptContent; // 保留原样
                }
                else
                {
                    if (selectedBashEnv!.EnvironmentType == BashEnvironmentType.Cmd)
                    {
                        // CMD 走 .cmd，保持 Windows 换行（CRLF）
                        extension = ".cmd";
                        finalContent = scriptContent; // 保留原样
                    }
                    else
                    {
                        // GitBash / WSL 走 .sh，强转 LF
                        extension = ".sh";
                        finalContent = scriptContent.Replace("\r\n", "\n");
                    }
                }

                string tempFile = Path.ChangeExtension(
                    Path.Combine(folder, Path.GetRandomFileName()), extension);
                // 统一转为 Unix 换行符写入，防止 Bash 报错 '\r' command not found
                File.WriteAllText(tempFile, finalContent);

                var psi = new ProcessStartInfo
                {
                    // 设置工作目录为用户选择的目录
                    WorkingDirectory = folder,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                // --- 3. 根据类型构建具体参数 ---

                if (type.Equals("powershell"))
                {
                    psi.FileName = "powershell.exe";
                    psi.Arguments = $"-NoExit -ExecutionPolicy Bypass -File \"{tempFile}\"";
                }
                else if (type.Equals("bash") && selectedBashEnv != null)
                {
                    psi.FileName = selectedBashEnv.ExecutablePath;

                    switch (selectedBashEnv.EnvironmentType)
                    {
                        case BashEnvironmentType.Cmd:
                            // 参数：/k 保持窗口， bash "Windows路径"
                            psi.Arguments = $"/k \"{tempFile}\"";
                            break;

                        case BashEnvironmentType.GitBash:
                            // Git Bash 模式：直接调用 bash.exe
                            // 路径处理：虽然 Git Bash 接受 Windows 路径，还是转为 / 比较保险
                            psi.FileName = selectedBashEnv.ExecutablePath; // bash.exe
                            string gitPath = tempFile.Replace("\\", "/");
                            psi.Arguments = $"-c \"source \\\"{gitPath}\\\"; exec bash\"";
                            break;

                        case BashEnvironmentType.Wsl:
                            // WSL 模式：最复杂，必须将 path 转为 /mnt/c/...
                            psi.FileName = selectedBashEnv.ExecutablePath; // wsl.exe
                            string wslPath = ConvertToWslPath(tempFile);
                            psi.Arguments = $"-e bash -c \"source \\\"{wslPath}\\\"; exec bash\"";
                            break;
                    }
                }

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行出错: {ex.Message}");
            }
        }
    }


    /// <summary>
    /// 将 Windows 路径转换为 Git Bash 格式 (C:\path -> /c/path)
    /// </summary>
    private string ConvertToGitBashPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return windowsPath;

        // 获取完整路径
        var fullPath = Path.GetFullPath(windowsPath);

        // C:\Users\xxx -> /c/Users/xxx
        if (fullPath.Length >= 2 && fullPath[1] == ':')
        {
            var driveLetter = char.ToLower(fullPath[0]);
            var pathWithoutDrive = fullPath.Substring(2).Replace('\\', '/');
            return $"/{driveLetter}{pathWithoutDrive}";
        }

        return fullPath.Replace('\\', '/');
    }

    /// <summary>
    /// 将 Windows 路径转换为 WSL 格式 (C:\path -> /mnt/c/path)
    /// </summary>
    private string ConvertToWslPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return windowsPath;

        // 获取完整路径
        var fullPath = Path.GetFullPath(windowsPath);

        // C:\Users\xxx -> /mnt/c/Users/xxx
        if (fullPath.Length >= 2 && fullPath[1] == ':')
        {
            var driveLetter = char.ToLower(fullPath[0]);
            var pathWithoutDrive = fullPath.Substring(2).Replace('\\', '/');
            return $"/mnt/{driveLetter}{pathWithoutDrive}";
        }

        return fullPath.Replace('\\', '/');
    }

    private void RenderCode(WpfRenderer wpfRenderer, IGrammar? grammar, StringLineGroup codeGroup)
    {
        var paragraph = new Paragraph();
        paragraph.BeginInit();
        paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
        wpfRenderer.Push(paragraph);
        if (grammar != null)
        {
            Tokenize(paragraph, codeGroup, grammar);
        }
        else
        {
            wpfRenderer.WriteRawLines(codeGroup);
        }

        paragraph.EndInit();
    }

    private static void Tokenize(IAddChild addChild, StringLineGroup stringLineGroup, IGrammar grammar)
    {
        IStateStack? ruleStack = null;
        if (stringLineGroup.Lines == null)
        {
            return;
        }

        for (var index = 0; index < stringLineGroup.Count; index++)
        {
            var blockLine = stringLineGroup.Lines[index];
            var line = blockLine.Slice.ToString();
            if (blockLine.Slice.Length == 0 || string.IsNullOrEmpty(line))
            {
                addChild.AddChild(new LineBreak());
                continue;
            }

            var result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
            ruleStack = result.RuleStack;
            foreach (var token in result.Tokens)
            {
                var lineLength = line.Length;
                var tokenStartIndex = token.StartIndex;
                var startIndex = (tokenStartIndex > lineLength) ? lineLength : tokenStartIndex;
                var endIndex = (token.EndIndex > lineLength) ? lineLength : token.EndIndex;
                var text = line.SubstringAtIndexes(startIndex, endIndex);
                var coloredRun = new TextmateColoredRun(text, token);
                coloredRun.SetResourceReference(FrameworkContentElement.StyleProperty,
                    TextMateCodeRenderer.TokenStyleKey);
                addChild.AddChild(coloredRun);
            }

            addChild.AddChild(new LineBreak());
        }
    }

    public string GetCopyText()
    {
        return CodeString;
    }
}