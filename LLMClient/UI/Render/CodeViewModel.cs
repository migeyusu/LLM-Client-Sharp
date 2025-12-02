using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using LLMClient.UI.Component.Utility;
using LLMClient.UI.ViewModel.Base;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Wpf;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors.Core;
using TextMateSharp.Grammars;

namespace LLMClient.UI.Render;

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

    private string _nameLower;

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