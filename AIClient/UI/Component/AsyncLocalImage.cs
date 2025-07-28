using System.Diagnostics;
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
            using (var fileStream = fileInfo.OpenRead())
            {
                try
                {
                    Source = fileStream.ToImageSource(extension);
                }
                catch (Exception e)
                {
                    Trace.TraceError(e.Message);
                }
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