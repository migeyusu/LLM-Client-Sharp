using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LambdaConverters;
using LLMClient.Data;
using LLMClient.Project;
using LLMClient.Rag;
using MaterialDesignThemes.Wpf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Images;
using UglyToad.PdfPig.Tokens;

namespace LLMClient.UI.Component.Converters;

internal static class SnapConverters
{
    public static readonly IValueConverter TraceToBrush =
        ValueConverter.Create<TraceEventType, Brush>(e =>
        {
            switch (e.Value)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    return Brushes.Red;
                case TraceEventType.Warning:
                    return Brushes.Orange;
                case TraceEventType.Information:
                case TraceEventType.Verbose:
                    return Brushes.Green;
                default:
                    return Brushes.Transparent;
            }
        });

    public static readonly IValueConverter ThicknessToDoubleConverter =
        ValueConverter.Create<Thickness, double>(e => e.Value.Left);

    public static readonly IValueConverter ProjectTaskStatusToBrush =
        ValueConverter.Create<ProjectTaskStatus, Brush>(args =>
        {
            switch (args.Value)
            {
                case ProjectTaskStatus.InProgress:
                    return Brushes.IndianRed;
                case ProjectTaskStatus.Completed:
                    return Brushes.Green;
                case ProjectTaskStatus.RolledBack:
                    return Brushes.Gray;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        });

    public static readonly IValueConverter EnumToDescriptionConverter =
        ValueConverter.Create<Enum, string>(e => e.Value.GetEnumDescription());

    private static readonly JsonSerializerOptions ObjectSerializeOptions =
        new JsonSerializerOptions() { WriteIndented = true };

    public static readonly IValueConverter ObjectToJsonConverter =
        ValueConverter.Create<object?, string>(e => e.Value != null
            ? JsonSerializer.Serialize(e.Value, Extension.DefaultJsonSerializerOptions)
            : string.Empty);

    public static readonly IValueConverter CountToVisibilityConverter =
        ValueConverter.Create<int, Visibility>(e => e.Value > 0 ? Visibility.Visible : Visibility.Collapsed);

    public static readonly IValueConverter EnumerableToVisibilityConverter =
        ValueConverter.Create<IEnumerable<object>?, Visibility>(e =>
        {
            if (e.Value == null || !e.Value.Any())
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        });

    public static readonly IValueConverter EnumerableToVisibilityInverseConverter =
        ValueConverter.Create<IEnumerable<object>?, Visibility>(e =>
        {
            if (e.Value == null || !e.Value.Any())
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        });

    public static readonly IValueConverter EnumerableToBoolConverter =
        ValueConverter.Create<IEnumerable<object>?, bool>(e => e.Value != null && e.Value.Any());

    public static readonly IValueConverter DocumentTypeToImageConverter =
        ValueConverter.Create<DocumentFileType?, ImageSource>(e =>
        {
            var dodgerBlue = Brushes.RoyalBlue;
            switch (e.Value)
            {
                case DocumentFileType.Text:
                    return PackIconKind.Text.ToImageSource(dodgerBlue);
                case DocumentFileType.Word:
                    return PackIconKind.MicrosoftWord.ToImageSource(dodgerBlue);
                case DocumentFileType.Pdf:
                    return PackIconKind.FilePdfBox.ToImageSource(dodgerBlue);
                case DocumentFileType.Excel:
                    return PackIconKind.MicrosoftExcel.ToImageSource(dodgerBlue);
                case DocumentFileType.Markdown:
                    return PackIconKind.LanguageMarkdownOutline.ToImageSource(dodgerBlue);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        });

    public static readonly IValueConverter TruncateTextConverter = ValueConverter.Create<string, string, int>((args =>
    {
        var text = args.Value;
        var maxLength = args.Parameter;
        return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
    }));

    public static readonly IValueConverter PdfImageConverter = ValueConverter.Create<IPdfImage, ImageSource>(args =>
    {
        var pdfImage = args.Value;
        if (pdfImage.TryGetPng(out var bytes))
        {
            using (var memoryStream = new MemoryStream(bytes))
            {
                return memoryStream.ToImageSource(".png");
            }
        }

        if (pdfImage.TryGetBytesAsMemory(out var memory))
        {
            using (var memoryStream = new MemoryStream(memory.ToArray()))
            {
                return memoryStream.ToImageSource(".jpg");
            }
        }

        if (pdfImage.ImageDictionary.TryGet(NameToken.Filter, out var token) && token.Equals(NameToken.DctDecode))
        {
            using (var memoryStream = new MemoryStream(pdfImage.RawBytes.ToArray()))
            {
                return memoryStream.ToImageSource(".jpg");
            }
        }

        return ThemedIcon.EmptyIcon.CurrentSource;
    });

    public static readonly IValueConverter Base64ToImageConverter =
        ValueConverter.Create<string, IList<ImageSource>>(args =>
        {
            var attachment = args.Value;
            var attachmentImages = new List<ImageSource>();
            if (string.IsNullOrEmpty(attachment))
            {
                return attachmentImages;
            }

            try
            {
                attachmentImages.AddRange(attachment.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(ImageExtensions.GetImageSourceFromBase64));
                return attachmentImages;
            }
            catch
            {
                return attachmentImages;
            }
        });

    //string empty or null will return collapsed visibility
    public static readonly IValueConverter StringToVisibilityConverter =
        ValueConverter.Create<string?, Visibility>(e =>
        {
            if (string.IsNullOrEmpty(e.Value))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        });

    /*public static readonly IValueConverter RAGIconConveter= ValueConverter.Create<IRagSource,PackIconKind>((args =>
    {
        IRagSource argsValue = args.Value;
        if (argsValue is RagFileBase ragFileBase)
        {
            switch (ragFileBase.FileType)
            {
                case DocumentFileType.Text:
                    return
                    break;
                case DocumentFileType.Word:
                    break;
                case DocumentFileType.Pdf:
                    break;
                case DocumentFileType.Excel:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }))*/
    public static readonly IValueConverter RagFileStatusToBoolConverter =
        ValueConverter.Create<RagStatus, bool>(e => e.Value == RagStatus.Constructed);
}