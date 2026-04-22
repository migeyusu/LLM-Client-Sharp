using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using LLMClient.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 流式阶段通过 MarkdownTextBlock 渲染，响应结束后使用 FlowDocument 呈现最终内容
/// </summary>
public class ClientResponseViewItem : ResponseViewItemBase, CommonCommands.ICopyable
{
    public ThemedIcon Icon
    {
        get { return Model?.Icon ?? ImageExtensions.APIThemedIcon; }
    }

    public string EndPointName
    {
        get { return Model?.Endpoint.Name ?? string.Empty; }
    }

    public string ModelName
    {
        get { return Model?.Name ?? string.Empty; }
    }

    public IEndpointModel? Model
    {
        get { return Client?.Model; }
    }

    public ILLMChatClient? Client { get; }

    public override bool IsInterrupt
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }

    /// <summary>
    /// tokens per second
    /// </summary>
    public override float TpS
    {
        get { return this.CalculateTps(); }
    }

    private int _respondingStateRefCount;

    public const string DIALOGAGENT = "Dialog";

    #region responding

    public virtual async Task<AgentTaskResult> Process(DefaultRequestContextBuilder contextBuilder,
        CancellationToken token = default)
    {
        var completedResult = AgentTaskResult.Empty;
        try
        {
            if (Client == null)
            {
                throw new InvalidOperationException("Client is null");
            }

            if (Client.IsResponding)
            {
                throw new InvalidOperationException("Client is busy");
            }

            AcquireRespondingState();
            ErrorMessage = null;
            Messages = [];
            using (CreateRequestTokenSource(token, out var liveToken))
            {
                var requestContext = await contextBuilder.BuildAsync(Client.Model, liveToken);
                requestContext.AgentId = DIALOGAGENT;
                completedResult = await ConsumeReactStepsAsync(
                    Client.SendRequestAsync(requestContext, liveToken));
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.Error(exception.Message, "响应中止");
            completedResult.Exception = exception;
        }
        finally
        {
            ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItemBase>(completedResult, this);
            PostOnPropertyChanged(nameof(TpS));
            InvalidateAsyncProperty(nameof(SearchableDocument));
            ReleaseRespondingState();
        }

        return completedResult;
    }

    #endregion

    public ClientResponseViewItem(ILLMChatClient client)
    {
        Client = client;
    }

    public void TriggerTextContentUpdate()
    {
        InvalidateAsyncProperty(nameof(SearchableDocument));
        RawTextContent = null;
        OnPropertyChanged(nameof(RawTextContent));
    }

    public string GetCopyText()
    {
        return RawTextContent ?? string.Empty;
    }

    internal void AcquireRespondingState()
    {
        if (Interlocked.Increment(ref _respondingStateRefCount) != 1)
        {
            return;
        }

        IsResponding = true;
    }

    internal void ReleaseRespondingState()
    {
        var remaining = Interlocked.Decrement(ref _respondingStateRefCount);
        if (remaining > 0)
        {
            return;
        }

        if (remaining < 0)
        {
            Interlocked.Exchange(ref _respondingStateRefCount, 0);
        }

        IsResponding = false;
    }
}