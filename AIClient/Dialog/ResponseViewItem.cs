using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using AutoMapper;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using LLMClient.UI;
using LLMClient.UI.Component;
using LLMClient.UI.Render;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    public ILLMChatModel? Model
    {
        get { return Client?.Model; }
    }

    public ILLMChatClient? Client { get; }

    public bool IsResponding
    {
        get => _isResponding;
        set
        {
            if (value == _isResponding) return;
            _isResponding = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Document));
        }
    }

    /// <summary>
    /// 是否中断
    /// </summary>
    public bool IsInterrupt
    {
        get => _isInterrupt;
        set
        {
            if (value == _isInterrupt) return;
            _isInterrupt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAvailableInContext));
        }
    }

    public long Tokens
    {
        get => _tokens;
        set
        {
            if (value == _tokens) return;
            _tokens = value;
            OnPropertyChanged();
        }
    }

    public int Latency
    {
        get => _latency;
        set
        {
            if (value == _latency) return;
            _latency = value;
            OnPropertyChanged();
        }
    }

    public int Duration
    {
        get => _duration;
        set
        {
            if (value == _duration) return;
            _duration = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (value == _errorMessage) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public double? Price
    {
        get => _price;
        set
        {
            if (Nullable.Equals(value, _price)) return;
            _price = value;
            OnPropertyChanged();
        }
    }

    private FlowDocument? _tempDocument;

    public FlowDocument? Document
    {
        get
        {
            if (this.IsResponding)
            {
                return _tempDocument;
            }

            return ResultDocument;
        }
    }

    private FlowDocument? _resultDocument;

    private FlowDocument? ResultDocument
    {
        get
        {
            if (this.ResponseMessages == null || !this.ResponseMessages.Any())
            {
                return null;
            }

            if (_resultDocument == null)
            {
                _resultDocument = new FlowDocument();
                var renderer = CustomRenderer.NewRenderer(_resultDocument);
                if (this.Annotations != null)
                {
                    foreach (var annotation in this.Annotations)
                    {
                        renderer.AppendItem(annotation,
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
                                renderer.AppendItem(reasoningContent,
                                    CustomRenderer.TextReasoningStyleKey);
                                break;
                            case TextContent textContent:
                                renderer.RenderRaw(textContent.Text);
                                break;
                            case FunctionCallContent functionCallContent:
                                renderer.AppendItem(functionCallContent,
                                    CustomRenderer.FunctionCallStyleKey);
                                break;
                            case FunctionResultContent functionResultContent:
                                renderer.AppendItem(functionResultContent,
                                    CustomRenderer.FunctionResultStyleKey);
                                break;
                            default:
                                Trace.TraceWarning($"Unknown content type: {content.GetType().FullName}");
                                break;
                        }
                    }
                }
            }

            return _resultDocument;
        }
    }

    private SearchableDocument? _searchableDocument = null;

    public SearchableDocument? SearchableDocument
    {
        get
        {
            if (_searchableDocument?.Document != Document)
            {
                _searchableDocument = null;
            }

            // 如果已经有了，则直接返回
            if (_searchableDocument == null)
            {
                var document = Document;
                if (document != null)
                {
                    _searchableDocument = new SearchableDocument(document);
                }
            }

            return _searchableDocument;
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
                    _textContent = string.Empty;
                }
            }

            return _textContent;
        }
    }


    /// <summary>
    /// response messages 来源于回复，但是为了前向兼容，允许基于raw生成
    /// </summary>
    public IList<ChatMessage>? ResponseMessages
    {
        get => _responseMessages;
        set
        {
            if (Equals(value, _responseMessages)) return;
            _responseMessages = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextContent));
        }
    }

    public ChatFinishReason? FinishReason
    {
        get => _finishReason;
        set
        {
            if (Nullable.Equals(value, _finishReason)) return;
            _finishReason = value;
            OnPropertyChanged();
        }
    }

    public IList<ChatAnnotation>? Annotations { get; set; }

    private bool _isManualValid = false;
    private bool _isInterrupt;
    private long _tokens;
    private int _latency;
    private int _duration;
    private string? _errorMessage;
    private double? _price;
    private IList<ChatMessage>? _responseMessages;
    private ChatFinishReason? _finishReason;
    private bool _isResponding;

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

    public ResponseViewItem(ILLMChatClient client)
    {
        Client = client;
    }

    #region responding

    public ICommand CancelCommand => new ActionCommand(o => { RequestTokenSource?.Cancel(); });

    public CancellationTokenSource? RequestTokenSource { get; private set; }

    public ObservableCollection<string> RespondingText { get; } = new();

    public async Task<CompletedResult> SendRequest(DialogContext context)
    {
        var completedResult = CompletedResult.Empty;
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

            _tempDocument = new FlowDocument();
            IsResponding = true;
            RespondingText.Clear();
            using (RequestTokenSource = new CancellationTokenSource())
            {
                using (var blockingCollection = new BlockingCollection<string>())
                {
                    var customRenderer = CustomRenderer.NewRenderer(_tempDocument);
                    var task = Task.Run(() =>
                    {
                        // ReSharper disable once AccessToDisposedClosure
                        RendererExtensions.StreamParse(blockingCollection,
                            (_, block) =>
                            {
                                Dispatch(() =>
                                {
                                    RespondingText.Clear();
                                    customRenderer.AppendMarkdownObject(block);
                                });
                            });
                    });
                    completedResult = await Client.SendRequest(context,
                        responseText =>
                        {
                            blockingCollection.Add(responseText);
                            Dispatch(() => RespondingText.Add(responseText));
                        },
                        cancellationToken: RequestTokenSource.Token);
                    blockingCollection.CompleteAdding();
                    await task.WaitAsync(CancellationToken.None);
                    ServiceLocator.GetService<IMapper>()!
                        .Map<IResponse, ResponseViewItem>(completedResult, this);
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
            this.ErrorMessage = exception.Message;
        }
        finally
        {
            RespondingText.Clear();
            _tempDocument = null;
            IsResponding = false;
        }

        return completedResult;
    }

    #endregion

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
        _resultDocument = null;
        _textContent = null;
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(SearchableDocument));
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
    public ICommand RecoverCommand => new ActionCommand(() =>
    {
        this.Text = _textContent.Text;
        this.HasEdit = false;
    });

    public bool HasEdit
    {
        get => _hasEdit;
        set
        {
            if (value == _hasEdit) return;
            _hasEdit = value;
            OnPropertyChanged();
        }
    }

    private string? _text;

    public string? Text
    {
        get => _text;
        set
        {
            if (value == _text) return;
            _text = value;
            OnPropertyChanged();
            HasEdit = true;
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
        if (!HasEdit)
        {
            return;
        }

        _textContent.Text = Text;
    }

    private readonly TextContent _textContent;
    private bool _hasEdit;

    public EditableTextContent(TextContent textContent, string? messageId)
    {
        this._textContent = textContent;
        this.MessageId = messageId;
        this._text = textContent.Text;
    }
}