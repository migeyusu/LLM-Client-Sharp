namespace LLMClient.Dialog;

public interface IRequestItem : IDialogItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    Guid InteractionId { get; set; }
}