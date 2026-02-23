using LLMClient.Dialog;
using LLMClient.Dialog.Models;
using Microsoft.Extensions.AI;

namespace LLMClient.Project;

/// <summary>
/// 用于agent 模式下的显示，区别于普通的response item，agent view item 不绑定具体的response，而是直接绑定agent的输出消息流
/// </summary>
public class AgentViewItem : BaseDialogItem
{
    public override long Tokens { get; }

    public override ChatRole Role
    {
        get { return ChatRole.Assistant; }
    }

    public override bool IsAvailableInContext => true;

    public override IAsyncEnumerable<ChatMessage> GetMessagesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}