using ImageMagick;
using Xunit.Abstractions;

namespace LLMClient.Test;

public class ImageTest
{
    private ITestOutputHelper _output;

    public ImageTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void Base64Uri()
    {
        /*data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEBLAEsAAD/6xdmSlAAAQAAAAEAABdcanVtYgAAAB5q*/
        var rawData = "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEBLAEsAAD/6xdmSlAAAQAAAAEAABdcanVtYgAAAB5q";
       // var testRaw = "base64:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA...";
        var tryCreate = System.Uri.TryCreate(rawData, UriKind.RelativeOrAbsolute, out var uri);
        _output.WriteLine($"TryCreate:{tryCreate}, Uri:{uri}");
        if (tryCreate)
        {
            _output.WriteLine("IsAbsoluteUri:" + uri!.IsAbsoluteUri);
            _output.WriteLine("Scheme:" + uri!.Scheme);
        }
    }

    [Fact]
    public void RelativeImage()
    {
        var testRaw = "image.png";
        var tryCreate = System.Uri.TryCreate(testRaw, UriKind.RelativeOrAbsolute, out var uri);
        _output.WriteLine($"TryCreate:{tryCreate}, Uri:{uri}");
        if (tryCreate)
        {
            _output.WriteLine("IsAbsoluteUri:" + uri!.IsAbsoluteUri);
        }
    }

    [Fact]
    public void Magic()
    {
        var supportedFormats = MagickNET.SupportedFormats;
        foreach (var format in supportedFormats)
        {
            _output.WriteLine($"{format.Format} - {format.Description}");
        }
    }
}