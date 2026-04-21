using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.CustomControl;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using Markdig;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LLMClient.Dialog.Models;

public class ResponseViewItemBase : BaseViewModel, IResponse
{
    public virtual long Tokens
    {
        get => Usage?.OutputTokenCount ?? 0;
    }

    /// <summary>
    /// tokens per second
    /// </summary>
    public virtual float TpS => this.CalculateTps();

    /// <summary>
    /// 上下文占比信息（无模型信息时返回空 ViewModel）
    /// </summary>
    public ContextUsageViewModel? LastContextUsage
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public int Latency
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public int Duration
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    public string? ErrorMessage
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public double? Price
    {
        get;
        set
        {
            if (Nullable.Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public UsageDetails? Usage
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tokens));
        }
    }

    /// <summary>
    /// 是否中断
    /// </summary>
    public virtual bool IsInterrupt
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
    public bool IsAvailableInContextSwitch
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    } = true;

    public bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    //标记为有效结果
    public static ICommand MarkValidCommand { get; } = new RelayCommand<ResponseViewItemBase>((o =>
    {
        if (o == null)
        {
            return;
        }

        o.IsManualValid = true;
    }));

    public static ICommand SetAsAvailableCommand { get; } = new RelayCommand<ResponseViewItemBase>(o =>
    {
        o?.SwitchAvailableInContext();
    });

    public static ICommand ShowRawResponseCommand { get; } = new RelayCommand<ResponseViewItemBase>(o =>
    {
        if (o == null)
        {
            return;
        }

        var window = new System.Windows.Window
        {
            Title = "原始文本",
            Width = 900,
            Height = 700,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
        };

        var scrollViewer = new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = o?.RawTextContent?.ToString() ?? string.Empty,
            IsReadOnly = true,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            VerticalContentAlignment = System.Windows.VerticalAlignment.Top,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
        };

        scrollViewer.Content = textBox;
        window.Content = scrollViewer;
        window.Show();
    });

    public ICommand EliminateFailedHistoryCommand { get; }

    public ICommand EliminateHistoryCommand { get; }

    /// <summary>
    /// 显示请求/响应的原始协议日志（HTTP headers、request body、response body 等）。
    /// </summary>
    public static ICommand ShowProtocolLogCommand { get; } = new RelayCommand<ResponseViewItemBase>(ShowProtocolLog);

    public static ICommand SaveProtocolLogCommand { get; } = new RelayCommand<ResponseViewItemBase>(SaveProtocolLog);

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

    private static void SaveProtocolLog(ResponseViewItemBase? item)
    {
        if (item?.ProtocolLog == null || string.IsNullOrEmpty(item.ProtocolLog))
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog()
        {
            Title = "保存请求/响应日志",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"ProtocolLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            File.WriteAllText(saveFileDialog.FileName, item.ProtocolLog);
        }
    }

    private static void ShowProtocolLog(ResponseViewItemBase? item)
    {
        if (item?.ProtocolLog == null || string.IsNullOrEmpty(item.ProtocolLog))
        {
            return;
        }

        var window = new System.Windows.Window
        {
            Title = "请求/响应日志",
            Width = 900,
            Height = 700,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
        };

        var scrollViewer = new System.Windows.Controls.ScrollViewer
        {
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = item.ProtocolLog,
            IsReadOnly = true,
            TextWrapping = System.Windows.TextWrapping.NoWrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            VerticalContentAlignment = System.Windows.VerticalAlignment.Top,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
        };

        scrollViewer.Content = textBox;
        window.Content = scrollViewer;
        window.Show();
    }

    public ICommand CancelCommand { get; }

    /// <summary>
    /// 响应过程中当前正在执行的操作（状态事件描述）
    /// </summary>
    public string? CurrentStatus
    {
        get;
        private set
        {
            if (value == field) return;
            field = value;
            PostOnPropertyChanged();
        }
    }

    public bool IsResponding
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public CancellationTokenSource? RequestTokenSource { get; set; }

    /// <summary>
    /// 请求/响应的原始协议日志，包含 HTTP headers、request body、response body 等详细信息。
    /// 用于调试查看，通过 <see cref="ShowProtocolLogCommand"/> 命令弹出日志窗口。
    /// </summary>
    public string? ProtocolLog { get; set; }

    /// <summary>
    /// 每轮 ReAct 循环的 ViewModel 列表
    /// </summary>
    public ObservableCollection<ReactLoopViewModel> Loops { get; } = [];

    public int LoopCount
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }


    /// <summary>
    /// response messages 来源于回复，但是为了前向兼容，允许基于raw生成
    /// </summary>
    public IEnumerable<ChatMessage> Messages
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public ChatFinishReason? FinishReason
    {
        get;
        set
        {
            if (Nullable.Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public IList<ChatAnnotation>? Annotations { get; set; }

    public string? RawTextContent
    {
        get
        {
            if (field == null)
            {
                if (Messages != null && Messages.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var message in Messages)
                    {
                        foreach (var messageContent in message.Contents)
                        {
                            if (messageContent is TextContent textContent)
                            {
                                sb.Append(textContent.Text);
                            }
                        }
                    }

                    field = sb.ToString();
                }
                else
                {
                    field = string.Empty;
                }
            }

            return field;
        }
        set
        {
            if (Equals(value, field)) return;
            field = value;
        }
    } = null;

    public bool CanEliminate
    {
        get
        {
            if (!IsResponding && IsInterrupt)
            {
                return false;
            }

            return true;
        }
    }


    protected ResponseViewItemBase()
    {
        CancelCommand = new ActionCommand(_ => CancelRequest(RequestTokenSource));
        EliminateHistoryCommand = new RelayCommand((() =>
            {
                if (Messages.Count() <= 1)
                {
                    return;
                }

                if (!CanEliminate)
                {
                    return;
                }

                //只保留最后一条消息
                Messages = [Messages.Last()];
                InvalidateAsyncProperty(nameof(SearchableDocument));
            }),
            () => !IsResponding && Messages.Any());
        EliminateFailedHistoryCommand = new RelayCommand((() =>
        {
            if (!Messages.Any())
            {
                return;
            }

            if (!CanEliminate)
            {
                return;
            }

            try
            {
                var messages = Messages.ToList();
                var segmentation = ReactHistorySegmenter.Segment(messages);
                var keptMessages = new List<ChatMessage>(segmentation.PreambleMessages);

                foreach (var round in segmentation.Rounds)
                {
                    var functionCalls = round.AssistantMessages
                        .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                        .ToList();

                    if (functionCalls.Count == 0)
                    {
                        keptMessages.AddRange(round.Messages);
                        continue;
                    }

                    var resultDict = round.ObservationMessages
                        .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
                        .ToDictionary(r => r.CallId);

                    var allFailed = functionCalls.All(call =>
                        resultDict.TryGetValue(call.CallId, out var result) && result.Exception != null);

                    if (!allFailed)
                    {
                        keptMessages.AddRange(round.Messages);
                    }
                }

                Messages = keptMessages;
                InvalidateAsyncProperty(nameof(SearchableDocument));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[EliminateFailedHistory Error]: {ex.Message}");
                MessageBoxes.Error(ex.Message, "清除失败历史失败");
            }
        }), () => !IsResponding && Messages.Any());
    }

    protected static async Task PopulateDocumentAsync(FlowDocument flowDocument,
        IEnumerable<ChatMessage>? responseMessages,
        IList<ChatAnnotation>? annotations)
    {
        if (responseMessages == null)
        {
            return;
        }

        var chatMessages = responseMessages.ToArray();
        if (chatMessages.Length == 0)
        {
            return;
        }

        flowDocument.Blocks.Clear();
        var renderer = CustomMarkdownRenderer.Rent(flowDocument);
        try
        {
            if (annotations != null)
            {
                foreach (var annotation in annotations)
                {
                    renderer.AppendExpanderItem(annotation,
                        CustomMarkdownRenderer.AnnotationStyleKey);
                }
            }

            var contents = chatMessages.SelectMany(m => m.Contents).ToList();
            var functionResults = contents.OfType<FunctionResultContent>().ToDictionary(r => r.CallId);

            foreach (var content in contents)
            {
                switch (content)
                {
                    case TextReasoningContent reasoningContent:
                        var markdownDocument = await Task.Run(() =>
                        {
                            var stringBuilder = new StringBuilder();
                            stringBuilder.AppendLine(ThinkBlockParser.OpenTag);
                            stringBuilder.AppendLine(reasoningContent.Text);
                            stringBuilder.AppendLine(ThinkBlockParser.CloseTag);
                            var s = stringBuilder.ToString();
                            return Markdown.Parse(s, CustomMarkdownRenderer.DefaultPipeline);
                        });
                        renderer.Render(markdownDocument);
                        break;
                    case TextContent textContent:
                        await renderer.RenderMarkdown(textContent.Text);
                        break;
                    case FunctionCallContent call:
                        functionResults.TryGetValue(call.CallId, out var result);
                        var interaction = new FunctionCallInteraction { Call = call, Result = result };
                        renderer.AppendExpanderItem(interaction, CustomMarkdownRenderer.FunctionInteractionStyleKey);
                        break;
                    case FunctionResultContent:
                        // Already handled via FunctionCallContent above
                        break;
                    default:
                        Trace.TraceWarning($"Unknown content type: {content.GetType().FullName}");
                        break;
                }
            }
        }
        finally
        {
            CustomMarkdownRenderer.Return(renderer);
        }
    }

    public async Task<FlowDocument?> CreateFullResponseDocumentAsync()
    {
        var flowDocument = new FlowDocument();
        await PopulateDocumentAsync(flowDocument, Messages, Annotations);
        return flowDocument;
    }

    /// <summary>
    /// 消费 IAsyncEnumerable&lt;ReactStep&gt; 流，将每轮循环写入 <paramref name="loops"/>
    /// 并累积为 <see cref="AgentTaskResult"/>。供 ClientResponseViewItem / LinearResponseViewItem 共用。
    /// </summary>
    internal async Task<AgentTaskResult> ConsumeReactStepsAsync(
        IAsyncEnumerable<ReactStep> steps)
    {
        var agentTaskResult = new AgentTaskResult();
        Loops.Clear();
        LoopCount = 0;
        CurrentStatus = null;
        await foreach (var step in steps)
        {
            LoopCount++;
            var loopVm = new ReactLoopViewModel { LoopNumber = LoopCount };
            Loops.Add(loopVm);

            await foreach (var evt in step)
            {
                switch (evt)
                {
                    case TextDelta t:
                        loopVm.ResponseBuffer.Add(t.Text);
                        loopVm.NotifyFirstLine();
                        break;
                    case ReasoningDelta r:
                        loopVm.ResponseBuffer.Add(r.Text);
                        break;
                    case FunctionCallStarted fc:
                    {
                        var msg = $"Function call: {fc.Call.Name}";
                        loopVm.ResponseBuffer.Add(msg + "\n");
                        loopVm.NotifyFirstLine();
                        CurrentStatus = msg;
                        break;
                    }
                    case FunctionCallCompleted fc:
                    {
                        var msg = fc.Error == null
                            ? $"Function result received: {fc.CallId}"
                            : $"Function call failed: {fc.CallId}";
                        loopVm.ResponseBuffer.Add(msg + "\n");
                        CurrentStatus = msg;
                        break;
                    }
                    case PermissionRequest pr:
                        var allowed = await InvokePermissionDialog.RequestAsync(pr.Content);
                        pr.Response.SetResult(allowed);
                        break;
                    case DiagnosticMessage dm:
                    {
                        var msg = $"[{dm.Level}] {dm.Message}";
                        loopVm.ResponseBuffer.Add(msg + "\n");
                        loopVm.NotifyFirstLine();
                        CurrentStatus = msg;
                        break;
                    }
                    case HistoryCompressionStarted compressionStarted:
                    {
                        var msg =
                            $"History compression: {FormatHistoryCompressionKind(compressionStarted.Kind)} started.";
                        loopVm.ResponseBuffer.Add(msg + "\n");
                        loopVm.NotifyFirstLine();
                        CurrentStatus = msg;
                        break;
                    }
                    case HistoryCompressionCompleted compressionCompleted:
                    {
                        var msg = compressionCompleted.Applied
                            ? $"History compression: {FormatHistoryCompressionKind(compressionCompleted.Kind)} applied."
                            : $"History compression: {FormatHistoryCompressionKind(compressionCompleted.Kind)} skipped.";
                        loopVm.ResponseBuffer.Add(msg + "\n");
                        loopVm.NotifyFirstLine();
                        CurrentStatus = msg;
                        break;
                    }
                }
            }

            var result = step.Result;
            if (result != null)
            {
                loopVm.ContextUsage = new ContextUsageViewModel(result.Usage, result.MaxContextTokens);
                loopVm.LatencyMs = result.Latency;
                loopVm.IsCompleted = true;
                loopVm.IsExpanded = false;
                agentTaskResult.Add(result);
            }
        }

        LastContextUsage = Loops.LastOrDefault(model => model.ContextUsage != null)?.ContextUsage;
        ProtocolLog = agentTaskResult.ProtocolLog?.ToString();
        return agentTaskResult;
    }

    private static string FormatHistoryCompressionKind(HistoryCompressionKind kind)
    {
        return kind switch
        {
            HistoryCompressionKind.PreambleSummary => "previous task context summary",
            HistoryCompressionKind.ObservationMasking => "observation masking",
            HistoryCompressionKind.InfoCleaning => "round info cleaning",
            HistoryCompressionKind.TaskSummary => "task summary",
            _ => kind.ToString(),
        };
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

    public CancellationTokenSource CreateRequestTokenSource(CancellationToken token, out CancellationToken liveToken)
    {
        this.RequestTokenSource = token != CancellationToken.None
            ? CancellationTokenSource.CreateLinkedTokenSource(token)
            : new CancellationTokenSource();
        liveToken = this.RequestTokenSource.Token;
        return this.RequestTokenSource;
    }

    internal static void CancelRequest(CancellationTokenSource? requestTokenSource)
    {
        if (requestTokenSource == null) return;
        try
        {
            if (!requestTokenSource.IsCancellationRequested)
            {
                requestTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Request already completed and CTS already disposed.
        }
    }
}