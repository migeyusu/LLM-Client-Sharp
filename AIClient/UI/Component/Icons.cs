using System.Reflection;
using System.Windows.Media.Imaging;
using LLMClient.Endpoints;

namespace LLMClient.UI.Component;

public class Icons
{
    static Dictionary<ModelIconType, BitmapImage> icons = new Dictionary<ModelIconType, BitmapImage>();

    public static BitmapImage? GetIcon(ModelIconType iconType)
    {
        if (iconType == ModelIconType.None)
            return null;
        if (!icons.TryGetValue(iconType, out var result))
        {
            result = new BitmapImage(new Uri(@"pack://application:,,,/"
                                             + Assembly.GetExecutingAssembly().GetName().Name
                                             + ";component/"
                                             + $"Resources/Images/llm/{iconType.ToString().ToLower()}.png",
                UriKind.Absolute));
            result.Freeze();
            icons.Add(iconType, result);
        }

        return result!;
    }
}