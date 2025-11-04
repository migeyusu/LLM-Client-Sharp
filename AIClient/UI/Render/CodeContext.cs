using System.IO;
using System.Windows.Documents;
using System.Windows.Input;
using LLMClient.UI.Component;
using Markdig.Helpers;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.UI.Render;

public class CodeContext : BaseViewModel, CommonCommands.ICopyable
{
    public CodeContext(StringLineGroup code, string? extension, string? name, IGrammar? grammar = null)
    {
        Extension = extension;
        Name = name ?? string.Empty;
        CodeGroup = code;
        Grammar = grammar;
        _codeStringLazy = new Lazy<string>(code.ToString);
        _codeDocumentLazy = new Lazy<FlowDocument>(() => TextMateCodeRenderer.Render(this));
    }

    public IGrammar? Grammar { get; }

    public string Name { get; }

    public string? Extension { get; }

    private Lazy<string> _codeStringLazy;
    public string CodeString => _codeStringLazy.Value;

    public StringLineGroup CodeGroup { get; }

    private Lazy<FlowDocument> _codeDocumentLazy;

    public FlowDocument CodeDocument
    {
        get => _codeDocumentLazy.Value;
    }

    public string? HtmlContent
    {
        get => _htmlContent;
        set
        {
            if (value == _htmlContent) return;
            _htmlContent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 0: text, 1:imagetext 2:image
    /// </summary>
    public int SelectedViewMode
    {
        get => _selectedViewMode;
        set
        {
            if (value == _selectedViewMode) return;
            _selectedViewMode = value;
            OnPropertyChanged();
            if (value != 0)
            {
                if (HtmlContent == null)
                {
                    HtmlContent = CodeString;
                }
            }
        }
    }

    private readonly string[] _supportedViewExtensions = new[] { "html" };

    private readonly string[] _supportedRunExtensions = new[] { "bash", "powershell", };
    private int _selectedViewMode = 0;
    private string? _htmlContent;

    public bool CanView
    {
        get { return !string.IsNullOrEmpty(Name) && _supportedViewExtensions.Contains(Name.ToLower().Trim()); }
    }

    public bool CanRun
    {
        get { return !string.IsNullOrEmpty(Name) && _supportedRunExtensions.Contains(Name.ToLower().Trim()); }
    }

    public ICommand RunCommand => new ActionCommand(o =>
    {
        try
        {
            //可以通过webview执行html
            var s = CodeString;
            if (!string.IsNullOrEmpty(s))
            {
                var tempFile = Path.GetTempFileName();
                var codeFile = Path.ChangeExtension(tempFile, ".html");
                File.Move(tempFile, codeFile);
                File.WriteAllText(codeFile, s);
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

    public string GetCopyText()
    {
        return CodeString;
    }
}

/*var code = content.TrimStart("cpp".ToCharArray()).TrimStart('\n');
        // 创建 ANTLR 输入流
        AntlrInputStream inputStream = new AntlrInputStream(codetest);

        // 使用生成的 CSharpLexer 分析词法
        CPP14Lexer lexer = new CPP14Lexer(inputStream);
        CommonTokenStream tokenStream = new CommonTokenStream(lexer);

        // 遍历所有 Token
        tokenStream.Fill();
        var enumerable = tokenStream.GetTokens();
        var enumerableCount = enumerable.Count;
        foreach (var token in enumerable)
        {
            // 根据 Token 类型输出不同颜色
            /*string highlighted = HighlightToken(token);
            Console.Write(highlighted);* /
        }*/
/*string HighlightToken(IToken token)
{
    // 获取 Token 类型
    string tokenText = token.Text;
    int tokenType = token.Type;
    Console.WriteLine(tokenType);
    // 根据 Token 类型添加样式
    switch (tokenType)
    {
        case CPP14Lexer.Class: // 这是 C# 的关键字
            return $"<span style='color: blue;'>{tokenText}</span>";
        case CPP14Lexer.And: // 字符串
            return $"<span style='color: green;'>{tokenText}</span>";
        case CPP14Lexer.Catch: // 注释
            return $"<span style='color: grey;'>{tokenText}</span>";
        default: // 其他类型，默认不加样式
            return tokenText;
    }
}*/