using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LambdaConverters;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class TextContentEditViewModel : BaseViewModel
{
    public static IMultiValueConverter MultiBooleanToVisibilityConverter =
        MultiValueConverter.Create<bool, Visibility>(args =>
            args.Values[0] && args.Values[1] ? Visibility.Visible : Visibility.Collapsed);

    public ICommand AddCodeFileCommand { get; protected set; }

    public string FinalText => Content.Text;

    protected TextContent Content { get; }

    public string? MessageId { get; }

    protected TextContentEditViewModel(TextContent textContent, string? messageId)
    {
        this.Content = textContent;
        this.MessageId = messageId;
    }

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