using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints;

namespace LLMClient.UI.Component;

public class Icons
{
    static Dictionary<ModelIconType, ImageSource> icons = new Dictionary<ModelIconType, ImageSource>();

    public static ImageSource? GetIcon(ModelIconType iconType)
    {
        if (iconType == ModelIconType.None)
            return null;
        if (!icons.TryGetValue(iconType, out var result))
        {
            //首先判断resouce中是否有该图标，图标可能以png或svg格式存在
            /*var componentPng = Assembly.GetExecutingAssembly().GetName().Name
                               + ";component/" + $"Resources/Images/llm/{iconType.ToString().ToLower()}.png";*/
            var componentSvg = Assembly.GetExecutingAssembly().GetName().Name
                               + ";component/" + $"Resources/Images/llm/{iconType.ToString().ToLower()}.svg";
            // var uriPng = new Uri(@"pack://application:,,,/" + componentPng, UriKind.Absolute);
            var uriSvg = new Uri(@"pack://application:,,,/" + componentSvg, UriKind.Absolute);
            var resourceStream = Application.GetResourceStream(uriSvg);
            if (resourceStream != null)
            {
                result = resourceStream.Stream.SVGStreamToImageSource();
                result.Freeze();
                icons.Add(iconType, result);
            }
        }


        return result;
    }
}