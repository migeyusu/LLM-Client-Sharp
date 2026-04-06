using System.Collections.ObjectModel;
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
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

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

    public ContextUsageViewModel ContextUsage
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
    public float TpS
    {
        get { return this.CalculateTps(); }
    }


    public ObservableCollection<AsyncPermissionViewModel> PermissionViewModels { get; } = [];

    /// <summary>
    /// 在响应过程中，临时存储文本内容，不持久化
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
                    IsReadOnly = true,
                    Text = o._history.ToString(),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        tempWindow.ShowDialog();
    });

    //标记为有效结果
    public static ICommand MarkValidCommand { get; } = new RelayCommand<ClientResponseViewItem>((o =>
    {
        if (o == null)
        {
            return;
        }

        o.IsManualValid = true;
    }));

    public static ICommand SetAsAvailableCommand { get; } = new RelayCommand<ClientResponseViewItem>(o =>
    {
        o?.SwitchAvailableInContext();
    });

    private readonly Lazy<SearchableDocument> _lazyDocument = new(() =>
    {
        return new SearchableDocument(new FlowDocument());
    });

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

    /// <summary>
    /// 手动标记为有效 
    /// </summary>
    public bool IsManualValid
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    } = false;


    /// <summary>
    /// 可以通过手动控制实现叠加的上下文可用性
    /// </summary>
    public bool IsAvailableInContextSwitch { get; set; } = true;

    public bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    #region responding

    public ICommand CancelCommand { get; }

    public CancellationTokenSource? RequestTokenSource { get; private set; }

    public virtual async Task<ChatCallResult> Process(DefaultDialogContextBuilder contextBuilder,
        CancellationToken token = default)
    {
        var completedResult = ChatCallResult.Empty;
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
            LoopCount = 0;
            Loops.Clear();
            Messages = [];
            _history.Clear();
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                var ct = RequestTokenSource.Token;
                var requestContext = await contextBuilder.BuildAsync(Client.Model, token);

                var totalUsage = new UsageDetails
                {
                    InputTokenCount = 0, OutputTokenCount = 0, TotalTokenCount = 0,
                };
                var allMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
                ChatFinishReason? finishReason = null;
                Exception? exception = null;
                int totalLatency = 0;
                int validCallTimes = 0;
                UsageDetails? lastSuccessfulUsage = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await foreach (var step in Client.SendRequestAsync(requestContext, ct))
                {
                    // BeginLoop
                    LoopCount++;
                    var loopVm = new ReactLoopViewModel { LoopNumber = LoopCount };
                    Loops.Add(loopVm);

                    await foreach (var evt in step.WithCancellation(ct))
                    {
                        switch (evt)
                        {
                            case TextDelta t:
                                loopVm.ResponseBuffer.Add(t.Text);
                                loopVm.NotifyFirstLine();
                                _history.Append(t.Text);
                                break;
                            case ReasoningDelta r:
                                loopVm.ResponseBuffer.Add(r.Text);
                                _history.Append(r.Text);
                                break;
                            case FunctionCallStarted fc:
                                loopVm.ResponseBuffer.Add($"Function call: {fc.Call.Name}\n");
                                loopVm.NotifyFirstLine();
                                break;
                            case FunctionCallCompleted fc:
                                loopVm.ResponseBuffer.Add(fc.Error == null
                                    ? $"Function result received: {fc.CallId}\n"
                                    : $"Function call failed: {fc.CallId}\n");
                                break;
                            case PermissionRequest pr:
                                var allowed = await InvokePermissionDialog.RequestAsync(pr.Content);
                                pr.Response.SetResult(allowed);
                                break;
                            case DiagnosticMessage dm:
                                loopVm.ResponseBuffer.Add($"[{dm.Level}] {dm.Message}\n");
                                _history.AppendLine($"[{dm.Level}] {dm.Message}");
                                break;
                        }
                    }

                    // EndLoop
                    var result = step.Result;
                    if (result != null)
                    {
                        loopVm.ContextUsage = new ContextUsageViewModel(
                            result.Usage, result.MaxContextTokens);
                        loopVm.LatencyMs = result.LatencyMs;
                        loopVm.IsCompleted = true;
                        loopVm.IsExpanded = false;

                        if (result.Usage != null)
                        {
                            totalUsage.Add(result.Usage);
                            lastSuccessfulUsage = result.Usage;
                        }

                        allMessages.AddRange(result.Messages);
                        finishReason = result.FinishReason;
                        exception ??= result.Exception;
                        totalLatency += result.LatencyMs;
                        if (result.Exception == null) validCallTimes++;
                    }
                }

                completedResult = new ChatCallResult
                {
                    Usage = totalUsage,
                    LastSuccessfulUsage = lastSuccessfulUsage,
                    Messages = allMessages,
                    FinishReason = finishReason,
                    Exception = exception,
                    Latency = totalLatency,
                    Duration = (int)Math.Ceiling(sw.ElapsedMilliseconds / 1000f),
                    ValidCallTimes = validCallTimes,
                };

                ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItemBase>(completedResult, this);
                OnPropertyChangedAsync(nameof(TpS));
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.Error(exception.Message, "响应失败");
            ErrorMessage = exception.Message;
        }
        finally
        {
            ReleaseRespondingState();
            InvalidateAsyncProperty(nameof(SearchableDocument));
        }

        return completedResult;
    }

    #endregion

    public ClientResponseViewItem(ILLMChatClient client)
    {
        Client = client;
        CancelCommand = new ActionCommand(o => { RequestTokenSource?.Cancel(); });
    }

    /// <summary>
    /// 切换在上下文中的可用性
    /// </summary>
    public void SwitchAvailableInContext()
    {
        if (!IsManualValid && IsInterrupt)
        {
            MessageEventBus.Publish("无法切换中断的响应，请先标记为有效");
            return;
        }

        IsAvailableInContextSwitch = !IsAvailableInContextSwitch;
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