using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Input;
using LLMClient.UI.Component;
using Microsoft.Xaml.Behaviors.Core;

namespace LLMClient.Dialog;

public class Attachment
{
    public Uri? OriUri { get; set; }

    [JsonIgnore]
    public string Name
    {
        get
        {
            if (OriUri?.IsFile == true)
            {
                return Path.GetFileName(OriUri.LocalPath);
            }

            return OriUri?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// 缓存的文件名
    /// </summary>
    public string? CachedFileName { get; set; }

    [JsonIgnore]
    public string CachedFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(CachedFileName))
            {
                return string.Empty;
            }

            return Path.GetFullPath(CachedFileName, ImageAttachmentCacheFolder);
        }
    }

    public AttachmentType Type { get; set; }

    [JsonIgnore]
    public string ImageAttachmentCacheFolder
    {
        get { return Path.GetFullPath(Path.Combine("Attachment", "Images")); }
    }

    public ICommand OpenFileCommand => new ActionCommand(o =>
    {
        if (Type == AttachmentType.Image)
        {
            string? filePath = null;
            var cachedFilePath = CachedFilePath;
            if (!string.IsNullOrEmpty(cachedFilePath) &&
                File.Exists(cachedFilePath))
            {
                filePath = cachedFilePath;
            }
            else
            {
                if (OriUri?.IsFile == true)
                {
                    var fileName = Path.GetFullPath(OriUri.LocalPath);
                    if (File.Exists(fileName))
                    {
                        filePath = fileName;
                    }
                }
            }

            if (filePath == null)
            {
                MessageEventBus.Publish("文件不存在！");
                return;
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
    });

    public bool EnsureCache()
    {
        try
        {
            if (OriUri == null)
            {
                throw new NullReferenceException("OriUri不能为空!");
            }

            switch (Type)
            {
                case AttachmentType.Image:
                    if (string.IsNullOrEmpty(CachedFileName) ||
                        !File.Exists(Path.GetFullPath(CachedFileName, ImageAttachmentCacheFolder)))
                    {
                        if (!OriUri.IsFile)
                        {
                            throw new NotSupportedException("图片附件必须是本地文件!");
                        }

                        this.CachedFileName =
                            Extension.CacheLocalFile(this.OriUri.LocalPath, ImageAttachmentCacheFolder);
                    }

                    break;
                default:
                    throw new NotSupportedException("不支持的附件类型: " + Type);
            }
        }
        catch (Exception e)
        {
            Trace.TraceError($"[Attachment] Error ensuring cache for {OriUri}: {e.Message}");
            return false;
        }

        return true;
    }
}