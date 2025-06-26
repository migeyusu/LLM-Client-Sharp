using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using LLMClient.Data;
using Microsoft.Extensions.AI;
using MimeTypes;

namespace LLMClient.UI.Dialog;

public class RequestViewItem : BaseViewModel, IDialogItem, IDialogPersistItem
{
    /// <summary>
    /// 标记一次请求-响应过程，和响应对应
    /// </summary>
    public Guid InteractionId { get; set; }

    [JsonPropertyName("MessageContent")] public string TextMessage { get; set; } = string.Empty;

    private ChatMessage? _message = null;

    public async Task<ChatMessage?> GetMessage()
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

                        var bytesAsync = await File.ReadAllBytesAsync(path);
                        _message.Contents.Add(new DataContent(bytesAsync.AsMemory(), mimeType));
                    }
                }
            }
        }

        return _message;
    }

    [JsonPropertyName("IsEnable")] public bool IsAvailableInContext { get; set; } = true;

    public List<Attachment>? Attachments { get; set; }

    [JsonIgnore]
    public long Tokens
    {
        //估计tokens
        get => (long)(TextMessage.Length / 2.5);
    }

    public RequestViewItem() : base()
    {
    }
}