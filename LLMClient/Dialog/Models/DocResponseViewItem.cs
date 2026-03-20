using System.Collections.Concurrent;
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
using LLMClient.Component.Render;
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
/// 支持flowdocument富文本渲染和交互
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

    private FlowDocument? _fullResponseDocument;

    private async Task<FlowDocument?> GetPreDocumentAsync()
    {
        if (this.IsResponding)
        {
            return _tempDocument;
        }

        if (_fullResponseDocument == null)
        {
            _fullResponseDocument = await CreateDocumentAsync(Messages, Annotations);
        }

        return _fullResponseDocument;
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

    private string? _rawTextContent = null;

    public string? RawTextContent
    {
        get
        {
            if (_rawTextContent == null)
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

                    _rawTextContent = sb.ToString();
                }
                else
                {
                    _rawTextContent = string.Empty;
                }
            }

            return _rawTextContent;
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

    public ObservableCollection<string> ResponseBuffer { get; } = [];

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
        _rawTextContent = null;
        InvalidateAsyncProperty(nameof(SearchableDocument));
        OnPropertyChanged(nameof(RawTextContent));
    }

    public string GetCopyText()
    {
        return RawTextContent ?? string.Empty;
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
                clearTail: () => Dispatch(() => responseViewItem.ResponseBuffer.Clear())
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
                    responseViewItem.ResponseBuffer.Add(message);
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
            var vm = new AsyncPermissionViewModel { Title = title, Content = message };
            _customRenderer.InsertExpanderItem(vm, CustomMarkdownRenderer.PermissionRequestStyleKey);
            return vm.Task;
        }

        public Task<bool> WaitForPermission(object content)
        {
            var vm = new AsyncPermissionViewModel { Content = content };
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