using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public class TextContentRawEditViewModel : TextContentEditViewModel
{
    private string? _text;

    public string? Text
    {
        get => _text;
        set
        {
            if (value == _text) return;
            _text = value;
            OnPropertyChanged();
            HasEdit = true;
        }
    }

    public TextContentRawEditViewModel(TextContent textContent, string? messageId) : base(textContent, messageId)
    {
        this._text = textContent.Text;
    }

    public override Task ApplyText()
    {
        if (!HasEdit)
        {
            return Task.CompletedTask;
        }

        Content.Text = Text;
        return Task.CompletedTask;
    }

    public override void AppendTempText(string tempText)
    {
        Text += tempText;
    }

    protected override void Rollback()
    {
        this.Text = Content.Text;
        this.HasEdit = false;
    }
}