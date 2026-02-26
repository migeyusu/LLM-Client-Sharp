using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.UserControls;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Wpf;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using TextMateSharp.Grammars;

namespace LLMClient.Component.Render;

public class CodeViewModel : BaseViewModel, CommonCommands.ICopyable
{
    // 渲染状态管理
    private Paragraph? _codeParagraph;

    public CodeViewModel(WpfRenderer renderer,
        StringLineGroup codeGroup, string? extension, string? name,
        IGrammar? grammar = null)
    {
        Extension = extension;
        Name = name ?? string.Empty;
        NameLower = Name.ToLower().Trim();
        CodeGroup = codeGroup;
        Grammar = grammar;
        _codeStringLazy = new Lazy<string>(codeGroup.ToString);
        InitializeRender(renderer, grammar, codeGroup);
    }

    public IGrammar? Grammar { get; }

    public string Name { get; }

    public string? Extension { get; }

    private readonly Lazy<string> _codeStringLazy;
    public string CodeString => _codeStringLazy.Value;

    public StringLineGroup CodeGroup { get; }

    private readonly string[] _supportedRunExtensions = new[] { "bash", "powershell", "html" };

    public string NameLower { get; }

    #region code extension

    public bool CanRun
    {
        get { return !string.IsNullOrEmpty(Name) && _supportedRunExtensions.Contains(NameLower); }
    }

    public static ICommand RunCommand { get; } = new RelayCommand<CodeViewModel>((async model =>
    {
        if (model == null)
        {
            return;
        }

        try
        {
            var nameLower = model.NameLower;
            //可以通过webview执行html
            var s = model.CodeString;
            if (!string.IsNullOrEmpty(s))
            {
                if (nameLower.Equals("html"))
                {
                    var webView2 = new WebView2()
                    {
                        Source = new Uri("about:blank"),
                    };
                    webView2.EnsureCoreWebView2Async().ConfigureAwait(true).GetAwaiter().OnCompleted(() =>
                    {
                        webView2.CoreWebView2.NavigateToString(s);
                    });
                    var window = new Window()
                    {
                        Title = "HTML Preview",
                        Width = 800,
                        Height = 600,
                        Content = webView2
                    };
                    window.Show();
                }
                else if (nameLower.Equals("bash") || nameLower.Equals("powershell"))
                {
                    ExecuteScriptLogic(s, nameLower);
                }
            }
        }
        catch (Exception e)
        {
            MessageEventBus.Publish(e.Message);
        }
    }));

    public static ICommand SaveCommand { get; } = new RelayCommand<CodeViewModel>(o =>
    {
        if (o == null)
        {
            return;
        }

        var s = o.GetCopyText();
        if (!string.IsNullOrEmpty(s))
        {
            try
            {
                var saveFileDialog = new SaveFileDialog()
                {
                    RestoreDirectory = true,
                };
                if (!string.IsNullOrEmpty(o.Extension))
                {
                    var defaultExt = o.Extension.TrimStart('.');
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

    private static void ExecuteScriptLogic(string scriptContent, string type)
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
    private static string ConvertToWslPath(string windowsPath)
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

    #endregion

    private void InitializeRender(WpfRenderer renderer, IGrammar? grammar, StringLineGroup codeGroup)
    {
        var paragraph = new Paragraph();
        paragraph.SetResourceReference(
            FrameworkContentElement.StyleProperty,
            Styles.CodeBlockStyleKey);

        // Push 将 paragraph 注册到 FlowDocument；
        // 返回后 Write 中的最终 renderer.Pop() 会关闭它
        renderer.Push(paragraph);

        if (grammar == null)
        {
            // 无语法高亮：WriteRawLines 需要 renderer 当前容器为 paragraph，
            // 必须同步执行，但它本身很快，不需要异步
            paragraph.BeginInit();
            renderer.WriteRawLines(codeGroup);
            paragraph.EndInit();
        }
        else
        {
            // 有语法高亮：分词耗时，放到后台；paragraph 暂为空，稍后填充
            _codeParagraph = paragraph;
            _ = TokenizeAndPopulateAsync(codeGroup, grammar);
        }
    }

    /// <summary>
    /// 后台分词 → UI 线程批量创建 Run。
    /// 全程不阻塞 UI，也不影响 DynamicResource 的懒更新特性。
    /// </summary>
    private async Task TokenizeAndPopulateAsync(StringLineGroup codeGroup, IGrammar grammar)
    {
        // 后台线程：纯 CPU 计算，不涉及任何 WPF 对象
        // List 中 null 表示换行，非 null 表示一个合并后的 Token
        var lines = await Task.Run(() => BuildMergedTokens(codeGroup, grammar));

        // UI 线程：批量创建 Run 并填充 Paragraph
        if (_codeParagraph == null) return;

        // BeginInit 阻止每次 Add 触发独立的布局计算，EndInit 后统一布局
        _codeParagraph.BeginInit();
        try
        {
            foreach (var token in lines)
            {
                if (token == null)
                {
                    _codeParagraph.Inlines.Add(new LineBreak());
                }
                else
                {
                    var run = new TextmateColoredRun(token.Text, token.Scopes);
                    // 保留 DynamicResource 绑定：进入可视树后自动获取当前主题
                    run.SetResourceReference(
                        FrameworkContentElement.StyleProperty,
                        TextMateCodeRenderer.TokenStyleKey);
                    _codeParagraph.Inlines.Add(run);
                }
            }
        }
        finally
        {
            _codeParagraph.EndInit();
        }
    }
    
    private static readonly TimeSpan TokenizeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 在后台线程执行分词并合并相邻 scopesKey 相同的 Token。
    ///
    /// 合并策略：
    /// - 相邻 token 的 scopesKey 完全相同 → 文本拼接，减少 Run 数量（通常可降低 50~80%）
    /// - scopesKey 不同 → 提交前一个合并组，开始新组
    /// - 每行末尾插入 null（换行标记）
    ///
    /// 返回值中 null 表示换行，非 null 表示合并后的 Token。
    /// </summary>
    private static List<MergedToken?> BuildMergedTokens(StringLineGroup codeGroup, IGrammar grammar)
    {
        var result = new List<MergedToken?>();
        if (codeGroup.Lines == null) return result;
        lock (grammar)
        {
            IStateStack? ruleStack = null;

            // 复用 StringBuilder，避免每行分配
            var sb = new System.Text.StringBuilder();

            string? pendingKey = null;
            List<string>? pendingScopes = null;

            void FlushPending()
            {
                if (pendingKey == null || sb.Length == 0) return;
                result.Add(new MergedToken(sb.ToString(), pendingScopes!, pendingKey));
                sb.Clear();
                pendingKey = null;
                pendingScopes = null;
            }

            for (var i = 0; i < codeGroup.Count; i++)
            {
                var blockLine = codeGroup.Lines[i];
                var line = blockLine.Slice.ToString();

                if (string.IsNullOrEmpty(line))
                {
                    FlushPending();
                    result.Add(null); // 换行
                    continue;
                }

                var tokenResult = grammar.TokenizeLine(line, ruleStack, TokenizeTimeout);
                ruleStack = tokenResult.RuleStack;

                var lineLen = line.Length;

                foreach (var token in tokenResult.Tokens)
                {
                    var start = Math.Min(token.StartIndex, lineLen);
                    var end = Math.Min(token.EndIndex, lineLen);
                    if (start >= end) continue;

                    var text = line.Substring(start, end - start);

                    // ScopesKey：用 \u001F（Unit Separator）拼接，该字符不会出现在正常代码中
                    var key = string.Join('\u001F', token.Scopes);

                    if (key == pendingKey)
                    {
                        // 与上一个 token 的 scopes 相同 → 直接追加文本，合并为一个 Run
                        sb.Append(text);
                    }
                    else
                    {
                        // scopes 变化 → 提交上一组，开始新组
                        FlushPending();
                        pendingKey = key;
                        pendingScopes = token.Scopes;
                        sb.Append(text);
                    }
                }

                // 每行结束后，先提交本行最后一组，再插入换行标记
                FlushPending();
                result.Add(null);
            }
        }

        return result;
    }


    /*private static void RenderCode(WpfRenderer wpfRenderer, IGrammar? grammar, StringLineGroup codeGroup)
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
    */

    public string GetCopyText()
    {
        return CodeString;
    }
}

/// <summary>
/// 合并后的 Token 数据，仅含纯数据字段，不依赖任何 WPF 类型。
/// 由后台线程生成，传递给 UI 线程批量创建 TextmateColoredRun。
/// </summary>
internal sealed class MergedToken
{
    /// <summary>合并后的文本（相邻 scopesKey 相同的 token 文本拼接）</summary>
    public string Text { get; }

    /// <summary>原始 scopes 数组（保留首个 token 的 scopes，相同 key 的 scopes 必然相同）</summary>
    public List<string> Scopes { get; }

    /// <summary>由 scopes 拼接的稳定 key，供 ThemeMatchCache 查询</summary>
    public string ScopesKey { get; }

    public MergedToken(string text, List<string> scopes, string scopesKey)
    {
        Text = text;
        Scopes = scopes;
        ScopesKey = scopesKey;
    }
}