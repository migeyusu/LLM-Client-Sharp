using LLMClient.Component.Utility;
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

    public override bool Check()
    {
        if (string.IsNullOrEmpty(Text))
        {
            MessageEventBus.Publish($"{MessageId}：文本内容不能为空");
            return false;
        }

        return true;
    }

    public override Task ApplyText()
    {
        if (!HasEdit)
        {
            return Task.CompletedTask;
        }

        TextContent.Text = Text;
        return Task.CompletedTask;
    }

    protected override void Rollback()
    {
        this.Text = TextContent.Text;
        this.HasEdit = false;
    }
}