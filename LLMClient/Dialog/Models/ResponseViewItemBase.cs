using System.Diagnostics;
using System.Text;
using System.Windows.Documents;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints.Messages;
using Markdig;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

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
        //todo: 回收
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

            foreach (var message in chatMessages)
            {
                foreach (var content in message.Contents)
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
                        case FunctionCallContent functionCallContent:
                            renderer.AppendExpanderItem(functionCallContent,
                                CustomMarkdownRenderer.FunctionCallStyleKey);
                            break;
                        case FunctionResultContent functionResultContent:
                            renderer.AppendExpanderItem(functionResultContent,
                                CustomMarkdownRenderer.FunctionResultStyleKey);
                            break;

                        default:
                            Trace.TraceWarning($"Unknown content type: {content.GetType().FullName}");
                            break;
                    }
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
}