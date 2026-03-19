using System.Runtime.CompilerServices;
using LLMClient.Agent;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;


public class RawResponseViewItem : ResponseViewItemBase
{
    
}

/// <summary>
/// 线性历史的ResponseViewItem
/// </summary>
public class LinearHistoryViewItem : MultiResponseViewItem<RawResponseViewItem>
{
    private long _tokensCount = 0;
    public override long Tokens => _tokensCount;

    public override bool IsAvailableInContext { get; } = true;

    private IAgent _agent;

    public LinearHistoryViewItem(IEnumerable<RawResponseViewItem> items, DialogSessionViewModel parentSession,
        IAgent agent) : base(items, parentSession)
    {
        _agent = agent;
    }

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var responseViewItemBase in this.Items)
        {
            var responseMessages = responseViewItemBase.ResponseMessages;
            if (responseMessages != null && responseMessages.Count > 0)
            {
                foreach (var responseMessage in responseMessages)
                {
                    yield return responseMessage;
                }
            }
        }
    }
}