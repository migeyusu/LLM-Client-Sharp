namespace LLMClient.Dialog.Models;

public interface IInteractionItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和请求对应
    /// </summary>
    Guid InteractionId { get; set; }
}