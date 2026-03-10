using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Render;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class TextContentRawEditViewModel : TextContentEditViewModel
{
    
    public string Text
    {
        get => Content.Text;
        set
        {
            if (value == Content.Text) return;
            Content.Text = value;
            OnPropertyChanged();
        }
    }

    public TextContentRawEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
    }

    protected override void AddCodeFile(object? o)
    {
        TextBoxBase? textBoxBase = null;
        if (o is TextBoxBase tb)
        {
            textBoxBase = tb;
        }

        textBoxBase ??= ((DependencyObject?)o)?.FindVisualChild<TextBoxBase>();

        if (textBoxBase == null)
        {
            return;
        }

        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Text files (*.*)|*.*",
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        InsertFilesAsTexts(openFileDialog.FileNames, textBoxBase);
    }
    
    public override Task ApplyText()
    {
        this.Content.Text = Text;
        return Task.CompletedTask;
    }

    public override void AppendTempText(string tempText)
    {
        Text += tempText;
    }

    public override void DropFiles(IEnumerable<string> filePaths, object? o)
    {
        TextBoxBase? textBoxBase = null;
        if (o is TextBoxBase tb)
        {
            textBoxBase = tb;
        }

        textBoxBase ??= ((DependencyObject?)o)?.FindVisualChild<TextBoxBase>();

        if (textBoxBase == null)
        {
            return;
        }
        
        InsertFilesAsTexts(filePaths, textBoxBase);
    }
    

    private static void InsertFilesAsTexts(IEnumerable<string> fileNames, TextBoxBase textBoxBase)
    {
        var settingsOptions = TextMateCodeRenderer.Settings.Options;
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine();
        foreach (var fileName in fileNames)
        {
            var extension = Path.GetExtension(fileName);
            var language = settingsOptions.GetLanguageByExtension(extension)?.Id ?? extension;
            stringBuilder.Append("```")
                .Append(language)
                .Append($" file=\"{fileName}\"");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(File.ReadAllText(fileName));
            stringBuilder.AppendLine("```");
        }

        textBoxBase.Focus();
        if (textBoxBase is TextBox textBox)
        {
            textBox.SelectedText = stringBuilder.ToString();
        }
    }
}