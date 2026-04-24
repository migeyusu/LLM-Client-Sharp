using LLMClient.Dialog.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

public interface IRequestContext
{
    /// <summary>
    /// 从接口上约定使用者不可添加，消除歧义
    /// </summary>
    IReadOnlyList<ChatMessage> ReadonlyHistory { get; }

    FunctionCallEngine FunctionCallEngine { get; }
    ChatOptions RequestOptions { get; }
    AdditionalPropertiesDictionary? TempAdditionalProperties { get; }
    bool AutoApproveAllInvocations { get; }
    bool ShowRequestJson { get; }

    AIContextProvider[]? ContextProviders { get; }

    /// <summary>
    /// dialogID 标识
    /// </summary>
    string DialogId { get; }
}

public sealed class RequestContext : IRequestContext
{
    public IReadOnlyList<ChatMessage> ReadonlyHistory
    {
        get { return ChatMessages; }
    }

    public required List<ChatMessage> ChatMessages { get; init; }

    public required FunctionCallEngine FunctionCallEngine { get; init; }

    public required ChatOptions RequestOptions { get; init; }

    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; init; }

    public bool AutoApproveAllInvocations { get; init; }

    public bool ShowRequestJson { get; set; }

    public AIContextProvider[]? ContextProviders { get; init; }

    public required string DialogId { get; init; }
}