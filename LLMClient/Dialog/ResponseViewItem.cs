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
using LLMClient.Component.CustomControl;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.Endpoints.Messages;
using Markdig;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Dialog;

public class ResponseViewItem : BaseDialogItem, IResponseViewItem, CommonCommands.ICopyable
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

    public ILLMModel? Model
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
            InvalidateAsyncProperty(nameof(SearchableDocument));
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

    public override long Tokens
    {
        get => Usage?.OutputTokenCount ?? 0;
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

    /// <summary>
    /// tokens per second
    /// </summary>
    public float TpS
    {
        get { return this.CalculateTps(); }
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

    public UsageDetails? Usage
    {
        get => _usage;
        set
        {
            if (Equals(value, _usage)) return;
            _usage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tokens));
        }
    }

    public ObservableCollection<string> TempResponseText { get; } = new();

    private FlowDocument? _tempDocument;

    private async Task<FlowDocument?> GetPreDocumentAsync()
    {
        if (this.IsResponding)
        {
            return _tempDocument;
        }

        if (_fullResponseDocument == null)
        {
            _fullResponseDocument = await CreateDocumentAsync(ResponseMessages, Annotations);
        }

        return _fullResponseDocument;
    }

    private FlowDocument? _fullResponseDocument;

    private static async Task<FlowDocument?> CreateDocumentAsync(IList<ChatMessage>? responseMessages,
        IList<ChatAnnotation>? annotations)
    {
        if (responseMessages == null || !responseMessages.Any())
        {
            return null;
        }

        var resultDocument = new FlowDocument();
        var renderer = CustomMarkdownRenderer.NewRenderer(resultDocument);
        if (annotations != null)
        {
            foreach (var annotation in annotations)
            {
                renderer.AppendExpanderItem(annotation,
                    CustomMarkdownRenderer.AnnotationStyleKey);
            }
        }

        foreach (var message in responseMessages)
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent reasoningContent:
                        var markdownDocument = await Task.Run(() =>
                        {
                            var stringBuilder = new StringBuilder();
                            stringBuilder.Append("\n:::think\n");
                            stringBuilder.Append(reasoningContent.Text);
                            stringBuilder.Append("\n:::\n");
                            var s = stringBuilder.ToString();
                            return Markdown.Parse(s, CustomMarkdownRenderer.DefaultPipeline);
                        });
                        renderer.Render(markdownDocument);
                        break;
                    case TextContent textContent:
                        var document = await Task.Run(() =>
                            Markdown.Parse(textContent.Text, CustomMarkdownRenderer.DefaultPipeline));
                        renderer.Render(document);
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

        return resultDocument;
    }

    public Task<FlowDocument?> CreateFullResponseDocumentAsync()
    {
        return CreateDocumentAsync(ResponseMessages, Annotations);
    }

    public SearchableDocument? SearchableDocument
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                var preDoc = await GetPreDocumentAsync();
                return preDoc != null ? new SearchableDocument(preDoc) : null;
            });
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
    private int _latency;
    private int _duration;
    private string? _errorMessage;
    private double? _price;
    private IList<ChatMessage>? _responseMessages;
    private ChatFinishReason? _finishReason;
    private bool _isResponding;
    private UsageDetails? _usage;

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

    /// <summary>
    /// 可以通过手动控制实现叠加的上下文可用性
    /// </summary>
    public bool IsAvailableInContextSwitch { get; set; } = true;

    public override bool IsAvailableInContext
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

    public async Task<CompletedResult> SendRequest(DialogContext context, CancellationToken token = default)
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

            _fullResponseDocument = null;
            ErrorMessage = null;
            _tempDocument = new FlowDocument();
            IsResponding = true;
            TempResponseText.Clear();
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                await using (var interactor = new ResponseViewItemInteractor(_tempDocument, this))
                {
                    completedResult = await Client.SendRequest(context, interactor,
                        cancellationToken: RequestTokenSource.Token);
                    ServiceLocator.GetService<IMapper>()!.Map<IResponse, ResponseViewItem>(completedResult, this);
                    //刷新tps
                    OnPropertyChangedAsync(nameof(TpS));
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
            TempResponseText.Clear();
            _tempDocument = null;
            IsResponding = false;
        }

        return completedResult;
    }

    #endregion

    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
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
        _fullResponseDocument = null;
        _textContent = null;
        InvalidateAsyncProperty(nameof(SearchableDocument));
        OnPropertyChanged(nameof(TextContent));
    }

    public string GetCopyText()
    {
        return TextContent ?? string.Empty;
    }

    private class ResponseViewItemInteractor : BaseViewModel, IInvokeInteractor, IAsyncDisposable
    {
        private readonly BlockingCollection<string> _blockingCollection = new();

        private readonly Task _task;

        private readonly CustomMarkdownRenderer _customRenderer;

        public ResponseViewItemInteractor(FlowDocument flowDocument, ResponseViewItem responseViewItem)
        {
            var responseViewItem1 = responseViewItem;
            _customRenderer = CustomMarkdownRenderer.NewRenderer(flowDocument);
            _task = Task.Run(() =>
            {
                // ReSharper disable once AccessToDisposedClosure
                RendererExtensions.StreamParse(_blockingCollection,
                    (_, block) =>
                    {
                        Dispatch(() =>
                        {
                            responseViewItem.TempResponseText.Clear();
                            _customRenderer.AppendMarkdownObject(block);
                        });
                    });
            });
            _outputAction = (message) =>
            {
                _blockingCollection.Add(message);
                Dispatch(() => responseViewItem1.TempResponseText.Add(message));
            };
        }

        private readonly Action<string> _outputAction;

        public void Info(string message)
        {
            _outputAction(message);
        }

        public void Error(string message)
        {
            _outputAction(message);
        }

        public void Warning(string message)
        {
            _outputAction(message);
        }

        public void Write(string message)
        {
            _outputAction(message);
        }

        public void WriteLine(string? message = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                _outputAction(Environment.NewLine);
            }
            else
            {
                _outputAction(message + Environment.NewLine);
            }
        }

        public Task<bool> WaitForPermission(string title, string message)
        {
            var permissionViewModel = new PermissionViewModel() { Title = title, Content = message };
            _customRenderer.AppendExpanderItem(permissionViewModel,
                CustomMarkdownRenderer.PermissionRequestStyleKey);
            return permissionViewModel.Task;
        }

        public Task<bool> WaitForPermission(object content)
        {
            var permissionViewModel = new PermissionViewModel() { Content = content };
            _customRenderer.AppendExpanderItem(permissionViewModel,
                CustomMarkdownRenderer.PermissionRequestStyleKey);
            return permissionViewModel.Task;
        }

        public async ValueTask DisposeAsync()
        {
            _blockingCollection.CompleteAdding();
            await _task.WaitAsync(CancellationToken.None);
            _blockingCollection.Dispose();
        }
    }
}