using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class TextContentEditViewModel : BaseViewModel
{
    public ICommand AddCodeFileCommand { get; }

    public string FinalText => Content.Text;

    protected TextContent Content { get; }

    public string? MessageId { get; }

    protected TextContentEditViewModel(TextContent textContent, string? messageId)
    {
        this.Content = textContent;
        this.MessageId = messageId;
        AddCodeFileCommand = new RelayCommand<object>(AddCodeFile);
    }

    protected abstract void AddCodeFile(object? obj);

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

    public abstract void DropFiles(IEnumerable<string> filePaths, object? o);
}