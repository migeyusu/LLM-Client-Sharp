using System.IO;
using System.Windows.Media;
using LLMClient.Data;

namespace LLMClient.UI.Component;

public class AsyncLocalImage : BaseViewModel
{
    private ImageSource? _source;

    public AsyncLocalImage(string imagePath)
    {
        ImageOriPath = imagePath;
        Task.Run(() =>
        {
            var fileInfo = new FileInfo(imagePath);
            if (!fileInfo.Exists)
            {
                return;
            }

            var extension = fileInfo.Extension;
            if (!ImageExtensions.IsSupportedImageExtension(extension))
            {
                return;
            }

            using (var fileStream = fileInfo.OpenRead())
            {
                Source = extension == ".svg" ? fileStream.SVGStreamToImageSource() : fileStream.ToDefaultImageSource();
            }
        });
    }

    public string ImageOriPath { get; }

    public string? CachePath { get; set; }

    public ImageSource? Source
    {
        get => _source;
        set
        {
            if (Equals(value, _source)) return;
            _source = value;
            OnPropertyChanged();
        }
    }
}