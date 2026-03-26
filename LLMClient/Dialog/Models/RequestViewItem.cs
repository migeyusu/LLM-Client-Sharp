using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Elsa.Extensions;
using LLMClient.Abstraction;
using LLMClient.Component.Render;
using LLMClient.Component.ViewModel;
using LLMClient.Configuration;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MimeTypes;

namespace LLMClient.Dialog.Models;

public class RequestViewItem : BaseDialogItem, IRequestItem, ISearchableDialogItem
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
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsDebugMode { get; set; }

    public bool AutoApproveAllInvocations { get; set; }

    public bool IsRawTextDisplayMode
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? FormattedTextMessage
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextMessage));
            InvalidateAsyncProperty(nameof(SearchableDocument));
            ParentSession?.IsDataChanged = true;
        }
    }

    public string? TextMessage
    {
        get => FormattedTextMessage ?? RawTextMessage;
    }

    public ICommand FormatTextCommand { get; }

    public SearchableDocument? SearchableDocument
    {
        get
        {
            return GetAsyncProperty(async () =>
            {
                var textMessage = TextMessage;
                if (!string.IsNullOrEmpty(textMessage))
                {
                    var flowDocument = new FlowDocument();
                    var renderer = CustomMarkdownRenderer.Rent(flowDocument);
                    try
                    {
                        await renderer.RenderMarkdown(textMessage);
                    }
                    finally
                    {
                        CustomMarkdownRenderer.Return(renderer);
                    }

                    return new SearchableDocument(flowDocument);
                }

                return null;
            });
        }
    }

    public List<IAIFunctionGroup>? FunctionGroups { get; set; }

    public bool HasFunctions
    {
        get
        {
            if (FunctionGroups == null || FunctionGroups.Count == 0)
            {
                return false;
            }

            return FunctionGroups.Any(group => group.AvailableTools?.Count > 0);
        }
    }

    public string? UserPrompt => RawTextMessage;

    public ISearchOption? SearchOption { get; set; }

    public ChatResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// 对Request附加的额外属性，不持久化
    /// </summary>
    public AdditionalPropertiesDictionary? TempAdditionalProperties { get; set; }

    public override bool IsAvailableInContext { get; } = true;

    public List<Attachment>? Attachments { get; set; }

    public bool HasAttachments
    {
        get => Attachments is { Count: > 0 };
    }

    public IRagSource[]? RagSources { get; set; }

    public bool HasRagSources
    {
        get => RagSources is { Length: > 0 };
    }

    public FunctionCallEngineType CallEngineType { get; set; }

    private ITokensCounter TokensCounter
    {
        get
        {
            field ??= ServiceLocator.GetService<ITokensCounter>()!;
            return field;
        }
    }

    /// <summary>
    /// 预估
    /// </summary>
    public override long Tokens
    {
        get { return GetAsyncProperty(async () => await TokensCounter.CountTokens(this.RawTextMessage), 0); }
    }

    public override ChatRole Role { get; } = ChatRole.User;

    public DialogSessionViewModel? ParentSession { get; set; }

    private readonly TextContent _textRequestContent;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rawTextMessage">直接设定rawtext message会触发tokens和doc重新计算</param>
    /// <param name="parentSession"></param>
    /// <exception cref="Exception"></exception>
    public RequestViewItem(string rawTextMessage, DialogSessionViewModel? parentSession = null)
    {
        _textRequestContent = new TextContent(rawTextMessage);
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
                    this.InvalidateAsyncProperty(nameof(SearchableDocument));
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

    private List<DataContent>? _dataContents;

    [MemberNotNull(nameof(_dataContents))]
    public async Task EnsureInitializeAsync(CancellationToken cancellationToken = default)
    {
        //一旦被创建，就不再改变，所以使用lazy模式
        if (_dataContents == null)
        {
            _dataContents = [];
            if (Attachments != null)
            {
                foreach (var attachment in Attachments)
                {
                    if (!attachment.EnsureCache())
                    {
                        throw new Exception("Failed to cache attachment: " + attachment.CachedFileName);
                    }

                    if (attachment.Type == AttachmentType.Image)
                    {
                        var path = attachment.CachedFilePath;
                        var extension = Path.GetExtension(path);
                        if (!MimeTypeMap.TryGetMimeType(extension, out string? mimeType))
                        {
                            throw new NotSupportedException("Unsupported image file extension: " + extension);
                        }

                        var bytesAsync = await File.ReadAllBytesAsync(path, cancellationToken);
                        _dataContents.Add(new DataContent(bytesAsync.AsMemory(), mimeType));
                    }
                }
            }
        }
    }

    public override IEnumerable<ChatMessage> Messages
    {
        get
        {
            if (_dataContents == null)
            {
                throw new NotSupportedException("No data content found.");
            }

            var chatMessage = new ChatMessage(ChatRole.User, [_textRequestContent]);
            chatMessage.Contents.AddRange(_dataContents);
            yield return chatMessage;
        }
    }

    public void TriggerTextContentUpdate()
    {
        ParentSession?.IsDataChanged = true;
        OnPropertyChanged(nameof(RawTextMessage));
        InvalidateAsyncProperty(nameof(Tokens));
        if (FormattedTextMessage != null)
        {
            FormattedTextMessage = null;
        }
        else
        {
            OnPropertyChanged(nameof(TextMessage));
            InvalidateAsyncProperty(nameof(SearchableDocument));
            ParentSession?.IsDataChanged = true;
        }
    }
}