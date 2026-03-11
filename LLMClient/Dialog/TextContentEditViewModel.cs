using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

public abstract class TextContentEditViewModel : BaseViewModel
{
    public ICommand AddCodeFileCommand { get; }

    public ICommand PastFileCommand => new RelayCommand<object>(enumerable =>
    {
        if (enumerable == null)
        {
            return;
        }

        DropFiles(enumerable as IEnumerable<string>, null);
    });

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePaths"></param>
    /// <param name="o">如果为空默认添加到尾部</param>
    public abstract void DropFiles(IEnumerable<string> filePaths, object? o);
}