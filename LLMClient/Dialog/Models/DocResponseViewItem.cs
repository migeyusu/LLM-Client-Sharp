using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using AutoMapper;
using CommunityToolkit.Mvvm.Input;
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
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 支持flowdocument富文本渲染的ResponseViewItem
/// </summary>
public class DocResponseViewItem : ResponseViewItemBase, CommonCommands.ICopyable
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
    public float TpS
    {
        get { return this.CalculateTps(); }
    }

    /// <summary>
    /// 在响应过程中，临时存储文本内容，不持久化
    /// </summary>
    private readonly StringBuilder _responseHistory = new();

    public static ICommand ShowTempResponseCommand { get; } = new RelayCommand<DocResponseViewItem>(o =>
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
                    Text = o._responseHistory.ToString(),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        tempWindow.ShowDialog();
    });

    //标记为有效结果
    public static ICommand MarkValidCommand { get; } = new RelayCommand<DocResponseViewItem>((o =>
    {
        if (o == null)
        {
            return;
        }

        o.IsManualValid = true;
    }));


    public static ICommand SetAsAvailableCommand { get; } = new RelayCommand<DocResponseViewItem>(o =>
    {
        o?.SwitchAvailableInContext();
    });

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

    public ObservableCollection<string> TempResponseText { get; } = new();

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

    public bool IsAvailableInContext
    {
        get { return (IsManualValid || !IsInterrupt) && IsAvailableInContextSwitch; }
    }

    public DocResponseViewItem(ILLMChatClient client)
    {
        Client = client;
    }

    #region responding

    public ICommand CancelCommand => new ActionCommand(o => { RequestTokenSource?.Cancel(); });

    public CancellationTokenSource? RequestTokenSource { get; private set; }

    public virtual async Task<ChatCallResult> Process(DialogContext context, CancellationToken token = default)
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

            _fullResponseDocument = null;
            ErrorMessage = null;
            _tempDocument = new FlowDocument();
            IsResponding = true;
            InvalidateAsyncProperty(nameof(SearchableDocument));
            _responseHistory.Clear();
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                await using (var interactor = new ResponseViewItemInteractor(_tempDocument, this))
                {
                    completedResult = await Client.SendRequest(context, interactor,
                        cancellationToken: RequestTokenSource.Token);
                    ServiceLocator.GetService<IMapper>()!.Map<IResponse, DocResponseViewItem>(completedResult, this);
                    //刷新tps
                    OnPropertyChangedAsync(nameof(TpS));
                }
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.Error(exception.Message, "发送失败");
            this.ErrorMessage = exception.Message;
        }
        finally
        {
            _tempDocument = null;
            IsResponding = false;
            InvalidateAsyncProperty(nameof(SearchableDocument));
        }

        return completedResult;
    }

    #endregion

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
        private readonly StreamingRenderSession _session;
        private readonly Action<string> _outputAction;

        public ResponseViewItemInteractor(FlowDocument flowDocument, DocResponseViewItem responseViewItem)
        {
            _customRenderer = CustomMarkdownRenderer.NewRenderer(flowDocument);

            _session = new StreamingRenderSession(
                flowDocument,
                clearTail: () => Dispatch(() => responseViewItem.TempResponseText.Clear())
            );

            _task = Task.Run(() =>
            {
                RendererExtensions.StreamParse(
                    _blockingCollection,
                    (_, block) => _session.OnBlockClosed(block));
            });

            _outputAction = message =>
            {
                if (_blockingCollection.IsAddingCompleted) return;

                _blockingCollection.Add(message);

                // Normal 优先级，确保在 Background 级别的 OnBlockClosed 之前执行
                Dispatch(() =>
                {
                    responseViewItem.TempResponseText.Add(message);
                    responseViewItem._responseHistory.Append(message);
                });
            };
        }

        public void Info(string message) => _outputAction(message);
        public void Error(string message) => _outputAction(message);
        public void Warning(string message) => _outputAction(message);
        public void Write(string message) => _outputAction(message);

        public void WriteLine(string? message = null)
        {
            _outputAction(string.IsNullOrEmpty(message)
                ? Environment.NewLine
                : message + Environment.NewLine);
        }

        public Task<bool> WaitForPermission(string title, string message)
        {
            var vm = new PermissionViewModel { Title = title, Content = message };
            _customRenderer.InsertExpanderItem(vm, CustomMarkdownRenderer.PermissionRequestStyleKey);
            return vm.Task;
        }

        public Task<bool> WaitForPermission(object content)
        {
            var vm = new PermissionViewModel { Content = content };
            _customRenderer.InsertExpanderItem(vm, CustomMarkdownRenderer.PermissionRequestStyleKey);
            return vm.Task;
        }

        public async ValueTask DisposeAsync()
        {
            _blockingCollection.CompleteAdding();

            // 等待 StreamParse 完成（含 CloseAll，所有 Closed 事件均已触发并 dispatch）
            await _task;

            // 使用 ContextIdle 确保所有 Background 级别的 OnBlockClosed 调度先执行完
            await _session.CompleteAsync();

            _session.Dispose();
            _blockingCollection.Dispose();
        }
    }
}