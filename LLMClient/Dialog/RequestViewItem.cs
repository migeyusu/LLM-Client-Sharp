using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel;
using LLMClient.Component.ViewModel.Base;
using LLMClient.Configuration;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.ToolCall;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MimeTypes;

namespace LLMClient.Dialog;

public class RequestViewItem : BaseViewModel, IRequestItem, IDialogPersistItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

    public string RawTextMessage
    {
        get => _textRequestContent.Text;
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

    public bool DisplayRawText
    {
        get => _displayRawText;
        set
        {
            if (value == _displayRawText) return;
            _displayRawText = value;
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
            OnPropertyChanged(nameof(Document));
            var dialogSessionViewModel = this.ParentSession;
            if (dialogSessionViewModel != null) dialogSessionViewModel.IsDataChanged = true;
        }
    }

    public string? TextMessage
    {
        get => FormattedTextMessage ?? RawTextMessage;
    }

    public ICommand FormatTextCommand { get; }

    private SearchableDocument? _document = null;

    public SearchableDocument? Document
    {
        get
        {
            if (_document != null)
            {
                return _document;
            }

            var textMessage = TextMessage;
            if (!string.IsNullOrEmpty(textMessage))
            {
                var flowDocument = new FlowDocument();
                var renderer = CustomRenderer.NewRenderer(flowDocument);
                renderer.RenderRaw(textMessage);
                _document = new SearchableDocument(flowDocument);
            }

            return _document;
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

            return FunctionGroups.Any(group => group.IsSelected != false && group.Functions.Count > 0);
        }
    }

    public ISearchOption? SearchOption { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// 对Request附加的额外属性，不持久化
    /// </summary>
    public AdditionalPropertiesDictionary TempAdditionalProperties { get; init; } = new();

    private ChatMessage? _message = null;

    private string? _formattedTextMessage;

    private bool _isFormatting;

    public bool IsAvailableInContext { get; } = true;

    public List<Attachment>? Attachments { get; set; }

    public bool HasAttachments
    {
        get => Attachments != null && Attachments.Count > 0;
    }

    public IList<IRagSource>? RagSources { get; set; }

    public bool HasRagSources
    {
        get => RagSources is { Count: > 0 };
    }

    public FunctionCallEngineType CallEngine { get; set; }

    private ITokensCounter? _tokensCounter;

    private ITokensCounter TokensCounter
    {
        get
        {
            _tokensCounter ??= ServiceLocator.GetService<ITokensCounter>()!;
            return _tokensCounter;
        }
    }

    /// <summary>
    /// 预估
    /// </summary>
    public long Tokens
    {
        get { return GetAsyncProperty(async () => await TokensCounter.CountTokens(this.RawTextMessage), 0); }
        set => SetAsyncProperty(value);
    }

    public DialogSessionViewModel? ParentSession { get; }

    private readonly TextContent _textRequestContent;
    private bool _displayRawText;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rawTextMessage">直接设定rawtext message会触发tokens和doc重新计算</param>
    /// <param name="parentSession"></param>
    /// <exception cref="Exception"></exception>
    public RequestViewItem(string rawTextMessage, DialogSessionViewModel? parentSession = null)
    {
        this._textRequestContent = new TextContent(rawTextMessage);
        ParentSession = parentSession;
        InteractionId = Guid.NewGuid();
        FormatTextCommand = new RelayCommand(async void () =>
        {
            try
            {
                IsFormatting = true;
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
                    _document = null;
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

    public async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_message == null)
        {
            //一旦被创建，就不再改变，所以使用lazy模式
            _message = new ChatMessage(ChatRole.User, [_textRequestContent]);
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

    public void TriggerTextContentUpdate()
    {
        _message = null;
        _formattedTextMessage = null;
        _document = null;
        OnPropertyChanged(nameof(RawTextMessage));
        OnPropertyChanged(nameof(FormattedTextMessage));
        OnPropertyChanged(nameof(TextMessage));
        OnPropertyChanged(nameof(Document));
        InvalidateAsyncProperty(nameof(Tokens));
        var dialogSessionViewModel = this.ParentSession;
        if (dialogSessionViewModel != null) dialogSessionViewModel.IsDataChanged = true;
    }
}