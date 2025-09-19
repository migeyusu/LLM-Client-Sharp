using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using LLMClient.MCP;
using LLMClient.UI;
using Microsoft.Extensions.AI;
using MimeTypes;

namespace LLMClient.Dialog;

public class RequestViewItem : BaseViewModel, IRequestItem, IDialogPersistItem, CommonCommands.ICopyable
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

    public string TextMessage { get; set; } = string.Empty;

    public List<CheckableFunctionGroupTree>? FunctionGroups { get; set; }

    public ISearchOption? SearchOption { get; set; }

    /// <summary>
    /// 对Request附加的额外属性
    /// </summary>
    public AdditionalPropertiesDictionary AdditionalProperties { get; set; } = new AdditionalPropertiesDictionary();

    private ChatMessage? _message = null;

    public bool IsAvailableInContext { get; } = true;

    public List<Attachment>? Attachments { get; set; }

    public IList<IRagSource>? RagSources { get; set; }

    public IThinkingConfig? ThinkingConfig { get; set; }

    public FunctionCallEngineType CallEngine { get; set; }

    public long Tokens
    {
        //估计tokens
        get => (long)(TextMessage.Length / 2.5);
    }

    public RequestViewItem() : base()
    {
        InteractionId = Guid.NewGuid();
    }

    public async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_message == null)
        {
            //一旦被创建，就不再改变，所以使用lazy模式
            _message = new ChatMessage(ChatRole.User, TextMessage);
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
        return TextMessage;
    }
}