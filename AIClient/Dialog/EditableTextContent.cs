using System.Windows.Input;
using LLMClient.UI;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class EditableTextContent : BaseViewModel
{
    public ICommand RecoverCommand => new ActionCommand(() =>
    {
        this.Text = _textContent.Text;
        this.HasEdit = false;
    });

    public bool HasEdit
    {
        get => _hasEdit;
        set
        {
            if (value == _hasEdit) return;
            _hasEdit = value;
            OnPropertyChanged();
        }
    }

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

    public string? MessageId { get; }

    public bool Check()
    {
        if (string.IsNullOrEmpty(Text))
        {
            MessageEventBus.Publish($"{MessageId}：文本内容不能为空");
            return false;
        }

        return true;
    }

    public void ApplyText()
    {
        if (!HasEdit)
        {
            return;
        }

        _textContent.Text = Text;
    }

    private readonly TextContent _textContent;
    private bool _hasEdit;

    public EditableTextContent(TextContent textContent, string? messageId)
    {
        this._textContent = textContent;
        this.MessageId = messageId;
        this._text = textContent.Text;
    }
}