using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Documents;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using Markdig;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public class FunctionCallInteraction
{
    public required FunctionCallContent Call { get; init; }
    public FunctionResultContent? Result { get; init; }
}

public class ResponseViewItemBase : BaseViewModel, IResponse
{
    public virtual long Tokens
    {
        get => Usage?.OutputTokenCount ?? 0;
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
            OnUsagePropertiesChanged();
        }
    }

    public UsageDetails? LastSuccessfulUsage
    {
        get;
        set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
            OnUsagePropertiesChanged();
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
    internal static async Task<AgentTaskResult> ConsumeReactStepsAsync(
        IAsyncEnumerable<ReactStep> steps,
        ObservableCollection<ReactLoopViewModel> loops,
        Action<int> setLoopCount,
        CancellationToken cancellationToken,
        int? fallbackMaxContextTokens = null)
    {
        var totalUsage = new UsageDetails
        {
            InputTokenCount = 0, OutputTokenCount = 0, TotalTokenCount = 0,
        };
        var allMessages = new List<ChatMessage>();
        ChatFinishReason? finishReason = null;
        Exception? exception = null;
        int totalLatency = 0;
        int validCallTimes = 0;
        UsageDetails? lastSuccessfulUsage = null;
        var sw = Stopwatch.StartNew();
        int loopCount = 0;

        loops.Clear();
        setLoopCount(0);

        await foreach (var step in steps.WithCancellation(cancellationToken))
        {
            loopCount++;
            setLoopCount(loopCount);
            var loopVm = new ReactLoopViewModel { LoopNumber = loopCount };
            loops.Add(loopVm);

            await foreach (var evt in step.WithCancellation(cancellationToken))
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
                        loopVm.NotifyFirstLine();
                        break;
                    case HistoryCompressionStarted compressionStarted:
                        loopVm.ResponseBuffer.Add($"History compression: {FormatHistoryCompressionKind(compressionStarted.Kind)} started.\n");
                        loopVm.NotifyFirstLine();
                        break;
                    case HistoryCompressionCompleted compressionCompleted:
                        loopVm.ResponseBuffer.Add(compressionCompleted.Applied
                            ? $"History compression: {FormatHistoryCompressionKind(compressionCompleted.Kind)} applied.\n"
                            : $"History compression: {FormatHistoryCompressionKind(compressionCompleted.Kind)} skipped.\n");
                        loopVm.NotifyFirstLine();
                        break;
                }
            }

            // EndLoop
            var result = step.Result;
            if (result != null)
            {
                var maxCtx = result.MaxContextTokens > 0 ? result.MaxContextTokens : fallbackMaxContextTokens;
                loopVm.ContextUsage = new ContextUsageViewModel(result.Usage, maxCtx);
                loopVm.LatencyMs = result.Latency;
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
                totalLatency += result.Latency;
                if (result.Exception == null) validCallTimes++;
            }
        }

        return new AgentTaskResult
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
    }

    /// <summary>
    /// 实例便捷方法 — 使用当前实例的 <see cref="Loops"/> 和 <see cref="LoopCount"/>。
    /// </summary>
    protected Task<AgentTaskResult> ConsumeReactStepsAsync(
        IAsyncEnumerable<ReactStep> steps,
        CancellationToken cancellationToken,
        int? fallbackMaxContextTokens = null)
        => ConsumeReactStepsAsync(steps, Loops, count => LoopCount = count, cancellationToken, fallbackMaxContextTokens);

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

    protected virtual void OnUsagePropertiesChanged()
    {
        
    }
}