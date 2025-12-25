using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.Utility;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory.AI;
using Microsoft.Xaml.Behaviors.Core;
using MimeTypes;

namespace LLMClient.Dialog;

public class RequestViewItem : BaseViewModel, IRequestItem, IDialogPersistItem, CommonCommands.ICopyable
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

    public string RawTextMessage
    {
        get => _rawTextMessage;
        set
        {
            if (value == _rawTextMessage) return;
            _rawTextMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextMessage));
            this.Tokens = 0;
        }
    }

    public bool IsFormatting
    {
        get => _isFormatting;
        set
        {
            if (value == _isFormatting) return;
            _isFormatting = value;
            OnPropertyChanged();
        }
    }

    public string? FormattedTextMessage
    {
        get => _formattedTextMessage;
        set
        {
            if (value == _formattedTextMessage) return;
            _formattedTextMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextMessage));
            OnPropertyChanged(nameof(SearchableDocument));
            var dialogSessionViewModel = this.ParentSession;
            if (dialogSessionViewModel != null) dialogSessionViewModel.IsDataChanged = true;
        }
    }

    public string? TextMessage
    {
        get => FormattedTextMessage ?? RawTextMessage;
    }

    public ICommand FormatTextCommand { get; }

    private SearchableDocument? _searchableDocument = null;

    public SearchableDocument? SearchableDocument
    {
        get
        {
            var textMessage = TextMessage;
            if (!string.IsNullOrEmpty(textMessage) && _searchableDocument == null)
            {
                var flowDocument = new FlowDocument();
                var renderer = CustomRenderer.NewRenderer(flowDocument);
                renderer.RenderRaw(textMessage);
                _searchableDocument = new SearchableDocument(flowDocument);
            }

            return _searchableDocument;
        }
    }


    public List<CheckableFunctionGroupTree>? FunctionGroups { get; set; }

    public bool HasFunctions
    {
        get
        {
            if (FunctionGroups == null || FunctionGroups.Count == 0)
            {
                return false;
            }

            foreach (var group in FunctionGroups)
            {
                if (group.IsSelected != false && group.Functions.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public ISearchOption? SearchOption { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// 对Request附加的额外属性，不持久化
    /// </summary>
    public AdditionalPropertiesDictionary TempAdditionalProperties { get; set; } = new AdditionalPropertiesDictionary();

    private ChatMessage? _message = null;
    private string? _formattedTextMessage;

    private bool _isFormatting;

    //使用0表示未计算，因为空字符串不能创建RequestViewItem本身
    private long _tokens = 0;
    private string _rawTextMessage = string.Empty;

    public bool IsAvailableInContext { get; } = true;

    public List<Attachment>? Attachments { get; set; }

    public bool HasAttachments
    {
        get => Attachments != null && Attachments.Count > 0;
    }

    public IList<IRagSource>? RagSources { get; set; }

    public bool HasRagSources
    {
        get => RagSources != null && RagSources.Count > 0;
    }

    public FunctionCallEngineType CallEngine { get; set; }

    /// <summary>
    /// 预估
    /// </summary>
    public long Tokens
    {
        get { return _tokens; }
        set
        {
            if (value == _tokens) return;
            _tokens = value;
            OnPropertyChanged();
        }
    }

    public DialogSessionViewModel? ParentSession { get; }

    public RequestViewItem(DialogSessionViewModel? parentSession = null)
    {
        ParentSession = parentSession;
        InteractionId = Guid.NewGuid();
        FormatTextCommand = new RelayCommand(async () =>
        {
            IsFormatting = true;
            try
            {
                var llmChatClient = ServiceLocator.GetService<GlobalOptions>()?.CreateTextFormatterClient();
                if (llmChatClient == null)
                {
                    throw new Exception("No available LLM endpoint for text formatting.");
                }

                var userPrompt =
                    await UserInputFormatter.FormatUserPromptAsync(llmChatClient, this,
                        this.ParentSession?.SystemPrompt, CancellationToken.None);
                if (!string.IsNullOrEmpty(userPrompt))
                {
                    _searchableDocument = null;
                    this.FormattedTextMessage = userPrompt;
                }
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to format text: " + e.Message, "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsFormatting = false;
            }
        });
    }

    /// <summary>
    /// 发送请求前，计算Tokens数量
    /// </summary>
    public async void CalculateTokensAsync(ITokensCounter counter)
    {
        if (this.Tokens != 0) return;
        if (this.RawTextMessage.Length == 0) return;
        this.Tokens = await counter.CountTokens(this.RawTextMessage);
    }

    public async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_message == null)
        {
            //一旦被创建，就不再改变，所以使用lazy模式
            _message = new ChatMessage(ChatRole.User, RawTextMessage);
            if (Attachments != null)
            {
                foreach (var attachment in Attachments)
                {
                    if (!attachment.EnsureCache())
                    {
                        Trace.TraceWarning("Failed to cache attachment: " + attachment.CachedFileName);
                        continue;
                    }

                    if (attachment.Type == AttachmentType.Image)
                    {
                        var path = attachment.CachedFilePath;
                        var extension = Path.GetExtension(path);
                        if (!MimeTypeMap.TryGetMimeType(extension, out string? mimeType))
                        {
                            Trace.TraceWarning("Unsupported image file extension: " + extension);
                            continue;
                        }

                        var bytesAsync = await File.ReadAllBytesAsync(path, cancellationToken);
                        _message.Contents.Add(new DataContent(bytesAsync.AsMemory(), mimeType));
                    }
                }
            }
        }

        yield return _message;
    }

    public string GetCopyText()
    {
        return RawTextMessage;
    }
}