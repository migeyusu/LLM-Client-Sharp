using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LambdaConverters;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public abstract class TextContentEditViewModel : BaseViewModel
{
    public static IMultiValueConverter MultiBooleanToVisibilityConverter =
        MultiValueConverter.Create<bool, Visibility>(args =>
            args.Values[0] && args.Values[1] ? Visibility.Visible : Visibility.Collapsed);

    private bool _isRollbackEnabled;

    public bool IsRollbackEnabled
    {
        get => _isRollbackEnabled;
        set
        {
            if (value == _isRollbackEnabled) return;
            _isRollbackEnabled = value;
            OnPropertyChanged();
        }
    }

    public ICommand RollbackCommand { get; }

    public string FinalText => Content.Text;

    protected TextContent Content { get; }

    public string? MessageId { get; }

    private bool _hasEdit;

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

    protected TextContentEditViewModel(TextContent textContent, string? messageId)
    {
        this.Content = textContent;
        this.MessageId = messageId;
        RollbackCommand = new ActionCommand(Rollback);
    }

    protected abstract void Rollback();

    public async Task<bool> ApplyAndCheck()
    {
        await ApplyText();
        if (string.IsNullOrEmpty(Content.Text))
        {
            MessageEventBus.Publish(string.IsNullOrEmpty(MessageId) ? $"{MessageId}：文本内容不能为空" : $"文本内容不能为空");
            return false;
        }

        return true;
    }

    public abstract Task ApplyText();

    public abstract void AppendTempText(string tempText);
}