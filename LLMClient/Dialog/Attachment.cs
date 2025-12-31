using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using LLMClient.Component.Utility;

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
    public static string ImageAttachmentCacheFolder
    {
        get { return Path.GetFullPath(Path.Combine("Attachment", "Images")); }
    }

    public static ICommand OpenFileCommand { get; } = new RelayCommand<Attachment>((o) =>
    {
        if (o == null)
        {
            return;
        }

        if (o.Type == AttachmentType.Image)
        {
            string? filePath = null;
            var cachedFilePath = o.CachedFilePath;
            if (!string.IsNullOrEmpty(cachedFilePath) &&
                File.Exists(cachedFilePath))
            {
                filePath = cachedFilePath;
            }
            else
            {
                var uri = o.OriUri;
                if (uri?.IsFile == true)
                {
                    var fileName = Path.GetFullPath(uri.LocalPath);
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

    public static Attachment CreateFromLocal(string path, AttachmentType type)
    {
        return new Attachment() { Type = type, OriUri = new Uri(path) };
    }

    public static Attachment? CreateFromClipBoards()
    {
        if (!Clipboard.ContainsImage())
        {
            return null;
        }

        var bitmapSource = Clipboard.GetImage();
        if (bitmapSource == null)
        {
            return null;
        }

        // 保存到缓存
        var tempFilePath = Extension.GetTempFilePath() + ".png";
        using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }

        return new Attachment() { Type = AttachmentType.Image, OriUri = new Uri(tempFilePath) };
    }

    public static Attachment CreateFromBinaryImage(byte[] imageData, string extension)
    {
        //save to temp path first
        var tempFilePath = Extension.GetTempFilePath() + extension;
        File.WriteAllBytes(tempFilePath, imageData);
        return new Attachment() { Type = AttachmentType.Image, OriUri = new Uri(tempFilePath) };
    }
}