using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints.Messages;
using LLMClient.Render;
using LLMClient.UI;
using LLMClient.UI.Component;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Xaml.Behaviors.Core;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Dialog;

public class ResponseViewItem : BaseViewModel, IResponseViewItem, CommonCommands.ICopyable
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

    public EditableResponseViewItem EditViewModel
    {
        get { return new EditableResponseViewItem(this); }
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

    public IList<ChatAnnotation>? Annotations { get; set; }

    private bool _isManualValid = false;

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

    public bool IsAvailableInContextSwitch { get; set; } = true;

    public bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    public ResponseViewItem(ILLMClient client)
    {
        Client = client;
    }

    public async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
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

    public void TriggerTextContentUpdate()
    {
        _flowDocument = null;
        _textContent = null;
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(TextContent));
    }

    public string GetCopyText()
    {
        return TextContent ?? string.Empty;
    }
}

public class EditableResponseViewItem : BaseViewModel
{
    public List<EditableTextContent> TextContents { get; } = new();

    public ICommand SaveCommand => new ActionCommand(() =>
    {
        if (TextContents.Any(textContent => !textContent.Check()))
        {
            return;
        }

        foreach (var textContent in TextContents)
        {
            textContent.ApplyText();
        }

        MessageEventBus.Publish("文本内容已更改");
        DialogHost.CloseDialogCommand.Execute(null, null);
        this._response.TriggerTextContentUpdate();
    });

    private readonly ResponseViewItem _response;

    public EditableResponseViewItem(ResponseViewItem response)
    {
        this._response = response;
        var messages = response.GetMessagesAsync(CancellationToken.None)
            .ToBlockingEnumerable();
        foreach (var message in messages)
        {
            var messageId = message.MessageId;
            foreach (var content in message.Contents)
            {
                if (content is TextContent textContent)
                {
                    TextContents.Add(new EditableTextContent(textContent, messageId));
                }
            }
        }
    }
}

public class EditableTextContent : BaseViewModel
{
    public ICommand RecoverCommand => new ActionCommand(() => { this.Text = _textContent.Text; });

    public string? Text
    {
        get => _text;
        set
        {
            if (value == _text) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public string? MessageId { get; }

    public bool Check()
    {
        if (string.IsNullOrEmpty(Text))
        {
            MessageEventBus.Publish($"{MessageId}：文本内容不能为空");
            return false;
        }

        return true;
    }

    public void ApplyText()
    {
        _textContent.Text = Text;
    }

    private readonly TextContent _textContent;
    private string? _text;

    public EditableTextContent(TextContent textContent, string? messageId)
    {
        this._textContent = textContent;
        this.MessageId = messageId;
        this.Text = textContent.Text;
    }
}