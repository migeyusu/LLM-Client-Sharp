using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Render;
using Microsoft.Win32;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class TextContentRawEditViewModel : TextContentEditViewModel
{
    private string? _originalText;

    protected override void Rollback()
    {
        Text = _originalText ?? string.Empty;
        this.HasEdit = false;
    }

    public override Task ApplyText()
    {
        return Task.CompletedTask;
    }

    public override void AppendTempText(string tempText)
    {
        Text += tempText;
    }

    public string Text
    {
        get => Content.Text;
        set
        {
            if (value == Content.Text) return;
            Content.Text = value;
            OnPropertyChanged();
            HasEdit = true;
        }
    }

    public TextContentRawEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
        _originalText = textContent.Text;
        AddCodeFileCommand = new RelayCommand<object>(AddCodeFile);
    }

    private void AddCodeFile(object? o)
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

        var settingsOptions = TextMateCodeRenderer.Settings.Options;
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine();
        foreach (var fileName in openFileDialog.FileNames)
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