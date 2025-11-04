using System.Windows.Markup;
using System.Windows.Media;

namespace LLMClient.UI.Component.Extension;

/// <summary>
/// 用于字体的示例代码
/// </summary>
[MarkupExtensionReturnType(typeof(FontFamily))]
public class SiYuanFontExtension : MarkupExtension
{
    private static readonly Lazy<FontFamily> _sourceLazy
        = new Lazy<FontFamily>(() =>
        {
            var fontFamilies = System.Windows.Media.Fonts.GetFontFamilies(
                new Uri("pack://application:,,,/LLMClient;component/Resources/Fonts/SiYuan/"), ".");
            return fontFamilies.First();
        });

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _sourceLazy.Value;
    }
}