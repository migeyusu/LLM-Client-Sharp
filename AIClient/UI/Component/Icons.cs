using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints;

namespace LLMClient.UI.Component;

public class Icons
{
    private static readonly Dictionary<ModelIconType, ImageSource> Cache = new Dictionary<ModelIconType, ImageSource>();

    public static ImageSource? GetIcon(ModelIconType iconType)
    {
        if (iconType == ModelIconType.None)
            return null;
        if (!Cache.TryGetValue(iconType, out var result))
        {
            //首先判断resouce中是否有该图标，图标可能以png或svg格式存在
            /*var componentSvg = Assembly.GetExecutingAssembly().GetName().Name
                               + ";component/" + $"Resources/Images/llm/{iconType.ToString().ToLower()}.svg";*/
            var uriPng = new Uri(@"pack://application:,,,/LLMClient;component/Resources/Images/llm/"
                                  + iconType.ToString().ToLower() + ".png", UriKind.Absolute);
            // var uriSvg = new Uri(@"pack://application:,,,/" + componentSvg, UriKind.Absolute);
            var resourceStream = Application.GetResourceStream(uriPng);
            if (resourceStream != null)
            {
                result = new BitmapImage(uriPng);
                result.Freeze();
                Cache.Add(iconType, result);
            }
        }

        return result;
    }
}

public class EnumToIconConverter: IValueConverter
{
    public static EnumToIconConverter Instance;
    
    static EnumToIconConverter()
    {
        Instance = new EnumToIconConverter();
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ModelIconType iconType)
        {
            return Icons.GetIcon(iconType);
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}