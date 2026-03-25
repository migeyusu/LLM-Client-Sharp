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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog.Models;

/// <summary>
/// 支持flowdocument富文本渲染和交互
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
    private readonly StringBuilder _responseHistory = new();

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
                    Text = o._responseHistory.ToString(),
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

    public ObservableCollection<string> ResponseBuffer { get; } = [];

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

            ErrorMessage = null;
            IsResponding = true;
            this.Messages = [];
            var searchableDocument = await WaitAsyncProperty<SearchableDocument>(nameof(SearchableDocument));
            var flowDocument = searchableDocument.Document;
            flowDocument.Blocks.Clear();
            _responseHistory.Clear();
            RequestTokenSource = token != CancellationToken.None
                ? CancellationTokenSource.CreateLinkedTokenSource(token)
                : new CancellationTokenSource();
            using (RequestTokenSource)
            {
                await using (var interactor = new ResponseViewItemInteractor(flowDocument, this))
                {
                    var requestContext = await contextBuilder.BuildAsync(Client.Model, token);
                    completedResult = await Client.SendRequest(requestContext, interactor,
                        cancellationToken: RequestTokenSource.Token);
                    ServiceLocator.GetService<IMapper>()!.Map<IResponse, ClientResponseViewItem>(completedResult, this);
                    //刷新tps
                    OnPropertyChangedAsync(nameof(TpS));
                }
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.Error(exception.Message, "响应失败");
            this.ErrorMessage = exception.Message;
        }
        finally
        {
            InvalidateAsyncProperty(nameof(SearchableDocument));
            IsResponding = false;
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


    private class ResponseViewItemInteractor : BaseViewModel, IInvokeInteractor, IAsyncDisposable
    {
        private readonly ClientResponseViewItem _responseViewItem;
        private readonly BlockingCollection<string> _blockingCollection = new();
        private readonly Task _task;
        private readonly StreamingRenderSession _session;
        private readonly Action<string> _outputAction;

        public ResponseViewItemInteractor(FlowDocument flowDocument, ClientResponseViewItem responseViewItem)
        {
            _responseViewItem = responseViewItem;
            _session = new StreamingRenderSession(
                flowDocument,
                clearTail: () => Dispatch(() => responseViewItem.ResponseBuffer.Clear())
            );

            _task = Task.Run(() =>
            {
                RendererExtensions.StreamParse(
                    _blockingCollection,
                    (_, block) => { Dispatch(() => _session.OnBlockClosed(block)); });
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

        public async Task<bool> WaitForPermission(string title, string message)
        {
            var permissionViewModel = new AsyncPermissionViewModel() { Title = title, Content = message };
            var permissionViewModels = _responseViewItem.PermissionViewModels;
            permissionViewModels.Add(permissionViewModel);
            var result = await permissionViewModel.Task;
            permissionViewModels.Remove(permissionViewModel);
            return result;
        }

        public async Task<bool> WaitForPermission(object content)
        {
            var vm = new AsyncPermissionViewModel { Content = content };
            var permissionViewModels = _responseViewItem.PermissionViewModels;
            permissionViewModels.Add(vm);
            var result = await vm.Task;
            permissionViewModels.Remove(vm);
            return result;
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