using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using Markdig.Wpf;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace LLMClient.Render;

public class CodeContext
{
    public CodeContext(string? extension, StringLineGroup code)
    {
        Extension = extension;
        CodeGroup = code;
    }

    public string? Extension { get; set; }
    public StringLineGroup CodeGroup { get; set; }
    public ICommand CopyCommand => new ActionCommand(o => { Clipboard.SetText(CodeGroup.ToString()); });
}

public class CodeRenderer : CodeBlockRenderer
{
    private readonly RegistryOptions _options;

    private readonly Registry _registry;

    readonly TextMateSharp.Themes.Theme _theme;

    public CodeRenderer(ThemeName themeName = ThemeName.Light)
    {
        _options = new RegistryOptions(themeName);
        _options.LoadFromLocalDir("Grammars");
        _registry = new Registry(_options);
        _theme = _registry.GetTheme();
    }

    protected override void Write(WpfRenderer renderer, CodeBlock obj)
    {
        var blockUiContainer = new BlockUIContainer();
        var contentControl = new ContentControl();
        contentControl.SetResourceReference(FrameworkElement.StyleProperty,
            MarkdownStyles.CodeBlockHeaderStyleKey);
        ((IAddChild)blockUiContainer).AddChild(contentControl);
        renderer.Push(blockUiContainer);
        renderer.Pop();
        var paragraph = new Paragraph();
        paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
        renderer.Push(paragraph);
        var written = false;
        if (obj is FencedCodeBlock fencedCodeBlock)
        {
            var extension = fencedCodeBlock.Info;
            contentControl.Content = new CodeContext(extension, fencedCodeBlock.Lines);
            if (extension != null)
            {
                var scope = _options.GetScopeByLanguageId(extension);
                if (scope == null)
                {
                    scope = _options.GetScopeByExtension("." + extension);
                }

                var grammar = _registry.LoadGrammar(scope);
                if (grammar != null)
                {
                    written = true;
                    CodeHighlight.TextMateColor(renderer, fencedCodeBlock, grammar, _theme);
                }
            }
        }

        if (!written)
        {
            renderer.WriteLeafRawLines(obj);
        }

        renderer.Pop();
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