using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shell;
using LambdaConverters;
using LLMClient.Data;
using LLMClient.Rag;
using LLMClient.Rag.Document;
using LLMClient.UI.Component.CustomControl;
using MaterialDesignThemes.Wpf;
using UglyToad.PdfPig.Content;

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

    public static readonly IValueConverter BooleanToProgressStateConverter =
        ValueConverter.Create<bool, TaskbarItemProgressState>(e =>
            e.Value ? TaskbarItemProgressState.Indeterminate : TaskbarItemProgressState.None);

    public static readonly IValueConverter StringArrayToStringConverter =
        ValueConverter.Create<string[], string>(e => string.Join(", ", e.Value));

    public static readonly IValueConverter ThicknessToDoubleConverter =
        ValueConverter.Create<Thickness, double>(e => e.Value.Left);

    public static readonly IValueConverter EnumToDescriptionConverter =
        ValueConverter.Create<Enum, string>(e => e.Value.GetEnumDescription());

    private static readonly JsonSerializerOptions ObjectSerializeOptions =
        new JsonSerializerOptions() { WriteIndented = true };

    public static readonly IValueConverter ObjectToJsonConverter =
        ValueConverter.Create<object?, string>(e => e.Value != null
            ? JsonSerializer.Serialize(e.Value, LLMClient.Extension.DefaultJsonSerializerOptions)
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
        return pdfImage.ToImageSource() ?? ThemedIcon.EmptyIcon.CurrentSource;
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

    public static readonly IValueConverter StringUnescapeConverter = ValueConverter.Create<string, string>(args =>
    {
        var rawJson = args.Value;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return rawJson;
        }

        try
        {
            // 配置序列化选项
            var options = new JsonSerializerOptions
            {
                // 1. 美化输出，带缩进和换行
                WriteIndented = true,
                // 2. 使用一个“宽松”的编码器，以避免将 ' " & < > + 以及中文等非ASCII字符转义成 \uXXXX
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // 使用JsonDocument解析，它非常高效，适合只读操作
            using var jsonDoc = JsonDocument.Parse(rawJson);

            // 将解析后的JSON文档对象重新序列化为美化后的字符串
            return JsonSerializer.Serialize(jsonDoc.RootElement, options);
        }
        catch (JsonException)
        {
            // 如果传入的字符串不是有效的JSON，则直接返回原始字符串，避免程序崩溃
            return rawJson;
        }
    });
}