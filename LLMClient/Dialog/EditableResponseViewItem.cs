using System.Windows.Input;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel.Base;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class DialogItemEditViewModel : BaseViewModel
{
    public List<EditableTextContent> TextContents { get; } = new();

    public ICommand SaveCommand => new ActionCommand(() =>
    {
        if (TextContents.Any(textContent => !textContent.Check()))
        {
            return;
        }

        foreach (var textContent in TextContents)
        {
            textContent.ApplyText();
        }

        MessageEventBus.Publish("文本内容已更改");
        DialogHost.CloseDialogCommand.Execute(null, null);
        this._response.TriggerTextContentUpdate();
    });

    private readonly IEditableDialogItem _response;

    public DialogItemEditViewModel(IEditableDialogItem response)
    {
        this._response = response;
        var messages = response.GetMessagesAsync(CancellationToken.None)
            .ToBlockingEnumerable();
        foreach (var message in messages)
        {
            var messageId = message.MessageId;
            foreach (var content in message.Contents)
            {
                if (content is TextContent textContent)
                {
                    TextContents.Add(new EditableTextContent(textContent, messageId));
                }
            }
        }
    }
}