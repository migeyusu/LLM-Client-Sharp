using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints.Messages;
using LLMClient.Render;
using LLMClient.UI.Component;
using Microsoft.Extensions.AI;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.UI.Dialog;

public class ResponseViewItem : BaseViewModel, IResponseViewItem
{
    public ThemedIcon Icon
    {
        get { return Model?.Icon ?? ImageExtensions.APIIcon; }
    }

    public string EndPointName
    {
        get { return Model?.Endpoint.Name ?? string.Empty; }
    }

    public string ModelName
    {
        get { return Model?.Name ?? string.Empty; }
    }

    public ILLMModel? Model
    {
        get { return Client?.Model; }
    }

    public ILLMClient? Client { get; }

    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt { get; set; }

    public long Tokens { get; set; }

    public int Latency { get; set; }

    public int Duration { get; set; }

    public string? ErrorMessage { get; set; }

    public double? Price { get; set; }

    private SearchableDocument? _flowDocument = null;

    public SearchableDocument? Document
    {
        get
        {
            if (this.ResponseMessages == null || !this.ResponseMessages.Any())
            {
                return null;
            }

            // 如果已经有了，则直接返回
            if (_flowDocument == null)
            {
                var flowDocument = new FlowDocument();
                var renderer = CustomRenderer.NewRenderer(flowDocument);
                if (this.Annotations != null)
                {
                    foreach (var annotation in this.Annotations)
                    {
                        renderer.RenderItem(annotation,
                            CustomRenderer.AnnotationStyleKey);
                    }
                }

                foreach (var message in ResponseMessages)
                {
                    foreach (var content in message.Contents)
                    {
                        switch (content)
                        {
                            case TextReasoningContent reasoningContent:
                                renderer.RenderItem(reasoningContent,
                                    CustomRenderer.TextReasoningStyleKey);
                                break;
                            case TextContent textContent:
                                renderer.RenderRaw(textContent.Text);
                                break;
                            case FunctionCallContent functionCallContent:
                                renderer.RenderItem(functionCallContent,
                                    CustomRenderer.FunctionCallStyleKey);
                                break;
                            case FunctionResultContent functionResultContent:
                                renderer.RenderItem(functionResultContent,
                                    CustomRenderer.FunctionResultStyleKey);
                                break;
                            default:
                                Trace.TraceWarning($"Unknown content type: {content.GetType().FullName}");
                                break;
                        }
                    }
                }

                _flowDocument = new SearchableDocument(flowDocument);
            }

            return _flowDocument;
        }
    }

    private string? _textContent = null;

    public string? TextContent
    {
        get
        {
            if (_textContent == null)
            {
                if (ResponseMessages != null && ResponseMessages.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var message in ResponseMessages)
                    {
                        foreach (var messageContent in message.Contents)
                        {
                            if (messageContent is TextContent textContent)
                            {
                                sb.Append(textContent.Text);
                            }
                        }
                    }

                    _textContent = sb.ToString();
                }
                else
                {
                    _textContent = String.Empty;
                }
            }

            return _textContent;
        }
    }

    /// <summary>
    /// response messages 来源于回复，但是为了前向兼容，允许基于raw生成
    /// </summary>
    public IList<ChatMessage>? ResponseMessages { get; set; }

    public ChatFinishReason? FinishReason { get; set; }

    private string? _responseWithoutThinking;
    private bool _isManualValid = false;

    public string? TextWithoutThinking
    {
        get
        {
            if (_responseWithoutThinking == null)
            {
                var textContent = this.TextContent;
                if (textContent != null)
                {
                    var index = textContent.IndexOf(ThinkingEndTag, StringComparison.Ordinal);
                    _responseWithoutThinking =
                        index > 0 ? textContent[(index + ThinkingEndTag.Length)..].Trim() : textContent;
                }
                else
                {
                    _responseWithoutThinking = string.Empty;
                }
            }

            return _responseWithoutThinking;
        }
    }

    private const string ThinkingEndTag = "</think>";

    public IList<ChatAnnotation>? Annotations { get; set; }

    public ResponseViewItem(ILLMClient client)
    {
        Client = client;
    }

    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (ResponseMessages != null && ResponseMessages.Any())
        {
            foreach (var chatMessage in ResponseMessages)
            {
                yield return chatMessage;
            }
        }
    }

    /// <summary>
    /// 手动标记为有效 
    /// </summary>
    public bool IsManualValid
    {
        get => _isManualValid;
        set
        {
            if (value == _isManualValid) return;
            _isManualValid = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }

    public bool IsAvailableInContext
    {
        get
        {
            if (IsManualValid)
            {
                return true;
            }

            return !IsInterrupt;
        }
    }
}