using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LLMClient.Abstraction;
using LLMClient.Data;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;
using MimeTypes;

namespace LLMClient.UI.Dialog;

public interface IRequestItem : IDialogItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    Guid InteractionId { get; set; }
}

public class RequestViewItem : BaseViewModel, IRequestItem, IDialogPersistItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

    [JsonPropertyName("MessageContent")] public string TextMessage { get; set; } = string.Empty;

    public List<IAIFunctionGroup>? FunctionGroups { get; set; }

    public ISearchService? SearchService { get; set; }

    /// <summary>
    /// 对Request附加的额外属性
    /// </summary>
    [JsonPropertyName("RequestAdditionalProperties")]
    public AdditionalPropertiesDictionary AdditionalProperties { get; set; } = new AdditionalPropertiesDictionary();

    private ChatMessage? _message = null;

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; } = true;

    public List<Attachment>? Attachments { get; set; }

    public IThinkingConfig? ThinkingConfig { get; set; }

    [JsonIgnore]
    public long Tokens
    {
        //估计tokens
        get => (long)(TextMessage.Length / 2.5);
    }

    public RequestViewItem() : base()
    {
    }

    public async IAsyncEnumerable<ChatMessage> GetMessages([EnumeratorCancellation] CancellationToken cancellationToken)
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
}