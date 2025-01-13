using System.Windows.Documents;
using Azure.AI.Inference;


namespace LLMClient;

public abstract class DialogItem
{
    public DialogItem()
    {
    }

    public abstract ChatRequestMessage? Message { get; }
}

public class EraseItem : DialogItem
{
    public override ChatRequestMessage? Message { get; } = null;
}

public class RequestItem : DialogItem
{
    public RequestItem(string messageContent) : base()
    {
        MessageContent = messageContent;
        UserMessage = new ChatRequestUserMessage(messageContent);
    }

    public ChatRequestUserMessage? UserMessage { get; set; }

    public string MessageContent { get; }
    public override ChatRequestMessage? Message => UserMessage;
}

public class ResponseItem : DialogItem
{
    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; }

    public string? ErrorMessage { get; set; }

    public FlowDocument? Document { get; set; }

    public ResponseItem(string message, FlowDocument? document, bool isInterrupt = false, string? errorMessage = null)
    {
        AssistantMessage = IsInterrupt ? null : new ChatRequestAssistantMessage(message);
        Document = document;
        IsInterrupt = isInterrupt;
        ErrorMessage = errorMessage;
    }

    public ChatRequestAssistantMessage? AssistantMessage { get; set; }

    public override ChatRequestMessage? Message => AssistantMessage;
}