using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Endpoints;
using LLMClient.Persistence;

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

    #region responding

    /// <summary>
    /// 核心 LLM Client 执行方法，支持重试（Reset）和继续（Append）两种模式。
    /// </summary>
    private async Task<AgentTaskResult> ExecuteWithClientAsync(
        DefaultRequestContextBuilder contextBuilder,
        CancellationToken token,
        ReactStepConsumeMode mode)
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
            if (mode == ReactStepConsumeMode.Reset)
            {
                ErrorMessage = null;
                Messages = [];
            }
            using (CreateRequestTokenSource(token, out var liveToken))
            {
                var requestContext = await contextBuilder.BuildAsync(Client.Model, liveToken);
                completedResult = await ConsumeReactStepsAsync(
                    Client.SendRequestAsync(requestContext, liveToken), mode);
            }
        }
        catch (Exception exception)
        {
            var errorMsg = mode == ReactStepConsumeMode.Append ? "继续失败" : "响应失败";
            MessageBoxes.Error(exception.Message, errorMsg);
            completedResult.Exception = exception;
        }
        finally
        {
            if (mode == ReactStepConsumeMode.Reset)
            {
                Mapper.Map<IResponse, ResponseViewItemBase>(completedResult, this);
            }
            else
            {
                MergeFrom(completedResult);
            }
            PostOnPropertyChanged(nameof(TpS));
            InvalidateAsyncProperty(nameof(SearchableDocument));
            ReleaseRespondingState();
        }

        return completedResult;
    }

    public virtual Task<AgentTaskResult> Process(DefaultRequestContextBuilder contextBuilder,
        CancellationToken token = default)
    {
        return ExecuteWithClientAsync(contextBuilder, token, ReactStepConsumeMode.Reset);
    }

    public Task<AgentTaskResult> Continue(DefaultRequestContextBuilder contextBuilder,
        CancellationToken token = default)
    {
        return ExecuteWithClientAsync(contextBuilder, token, ReactStepConsumeMode.Append);
    }

    public Task<AgentTaskResult> Retry(DefaultRequestContextBuilder contextBuilder,
        CancellationToken token = default)
    {
        return ExecuteWithClientAsync(contextBuilder, token, ReactStepConsumeMode.Reset);
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
