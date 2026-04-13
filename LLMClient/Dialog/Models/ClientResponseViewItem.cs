using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Endpoints;
using LLMClient.Persistance;
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

    public override ContextUsageViewModel ContextUsage
    {
        get
        {
            var maxContextSize = Model?.MaxContextSize;
            return new ContextUsageViewModel(
                LastSuccessfulUsage,
                maxContextSize > 0 ? maxContextSize : null);
        }
    }

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

    /// <summary>
    /// 缓存 ChatContext.InteractionHistory，用于调试查看
    /// </summary>
    private readonly StringBuilder _history = new();

    private int _respondingStateRefCount;

    public static ICommand ShowTempResponseCommand { get; } = new RelayCommand<ClientResponseViewItem>(o =>
    {
        if (o == null)
        {
            return;
        }

        var tempWindow = new Window()
        {
            Content = new ScrollViewer()
            {
                Content = new TextBox()
                {
                    TextAlignment = TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    IsReadOnly = true,
                    Text = o._history.ToString(),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        tempWindow.ShowDialog();
    });


    private readonly Lazy<SearchableDocument> _lazyDocument = new(() => new SearchableDocument(new FlowDocument()));

    public SearchableDocument? SearchableDocument
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                var document = _lazyDocument.Value;
                await PopulateDocumentAsync(document.Document, Messages, Annotations);
                document.OnDocumentRefresh();
                return document;
            });
        }
    }

    #region responding

    public virtual async Task<AgentTaskResult> Process(DefaultDialogContextBuilder contextBuilder,
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
            RequestTokenSource = CreateRequestTokenSource(token);
            using (RequestTokenSource)
            {
                var ct = RequestTokenSource.Token;
                var requestContext = await contextBuilder.BuildAsync(Client.Model, ct);
                completedResult = await ConsumeReactStepsAsync(
                    Client.SendRequestAsync(requestContext, ct));
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.Error(exception.Message, "响应中止");
            completedResult.Exception = exception;
        }
        finally
        {
            _history.Clear();
            _history.Append(completedResult.ProtocolLog);
            ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItemBase>(completedResult, this);
            PostOnPropertyChanged(nameof(TpS));
            InvalidateAsyncProperty(nameof(SearchableDocument));
            RequestTokenSource = null;
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
        Messages.ClearTokensCounterTag();
        InvalidateAsyncProperty(nameof(SearchableDocument));
        RawTextContent = null;
        OnPropertyChanged(nameof(RawTextContent));
    }

    public string GetCopyText()
    {
        return RawTextContent ?? string.Empty;
    }

    protected override void OnUsagePropertiesChanged()
    {
        base.OnUsagePropertiesChanged();
        OnPropertyChanged(nameof(ContextUsage));
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