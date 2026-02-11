using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using LatexToMathML;
using LLMClient.Data;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using SvgMath;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;

namespace LLMClient.Component.Render;

public class FormulaRenderCache
{
    public static FormulaRenderCache Instance { get; } = new FormulaRenderCache();

    private readonly ConcurrentDictionary<CacheKey, Lazy<Task<ImageSource>>> _cache;
    private readonly TexFormulaParser _parser;

    private record CacheKey(string Formula, double FontSize);

    public FormulaRenderCache()
    {
        _parser = WpfTeXFormulaParser.Instance;
        _cache = new ConcurrentDictionary<CacheKey, Lazy<Task<ImageSource>>>();
    }

    public Task<ImageSource> TryGet(string formula, double scale = 20.0)
    {
        var key = new CacheKey(formula, scale);
        return _cache.GetOrAdd(key, k => new Lazy<Task<ImageSource>>(() => ValueFactory(k))).Value;
    }

    private static readonly FontFamily MathFontFamily = new FontFamily("Cambria Math");

    private async Task<ImageSource> ValueFactory(CacheKey cacheKey)
    {
        // return RenderLatex(cacheKey.Formula);
        try
        {
            var texFormula = _parser.Parse(cacheKey.Formula);
            // 创建包含字体信息的环境
            var environment = WpfTeXEnvironment.Create(
                scale: cacheKey.FontSize);
            return texFormula.RenderToBitmap(environment, scale: cacheKey.FontSize, dpi: 96 * 2);
        }
        catch (TexException)
        {
            //not a valid formula, not log
            // return new CachedFormulaResult(cacheKey.Formula, ex);
        }
        catch (Exception)
        {
            //not a valid formula, not log
            // return new CachedFormulaResult(cacheKey.Formula, ex);
        }

        try
        {
            var renderService = await MathJaxLatexRenderService.InstanceAsync();
            return await renderService.RenderAsync(cacheKey.Formula, cacheKey.FontSize);
        }
        catch (Exception)
        {
            // ignored
        }

        return ImageExtensions.CreateImageSourceFromText(cacheKey.Formula, MathFontFamily, 20, Brushes.Black);
    }

    private readonly Lazy<MathMlToSvg> _mml = new((() =>
    {
        var fullPath = Path.GetFullPath("svgmath.xml");
        return new MathMlToSvg(new MathConfig(fullPath));
    }));

    private const string LatexBegin = @"\begin{document} $";
    private const string LatexEnd = @"$ \end{document}";

    private ImageSource RenderLatex(string formula)
    {
        formula = LatexBegin + formula + LatexEnd;
        var parser = new LatexParser(formula, new LatexMathToMathMLConverter());
        var root = parser.Root;
        var convert = root.Convert();
        var xElement = XElement.Parse(convert);
        var svg = _mml.Value.MakeSvg(xElement);
        return ToImageSource(svg);
    }

    /// <summary>
    /// 将 XElement 里的 SVG 转成 ImageSource（保持矢量）
    /// </summary>
    public static ImageSource ToImageSource(XElement svgElement)
    {
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(
            svgElement.ToString(SaveOptions.DisableFormatting)));
        var settings = new WpfDrawingSettings
        {
            IncludeRuntime = false, // 不生成运行时 XAML 注入
            TextAsGeometry = false,
        };

        var reader = new FileSvgReader(settings);
        var drawing = reader.Read(memoryStream);
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}

public class MathMlToSvg
{
    private readonly MathConfig? _mMathConfig;

    public MathMlToSvg(MathConfig mathConfig)
    {
        this._mMathConfig = mathConfig;
    }

    public XElement MakeSvg(XElement mmlContent)
    {
        var mMathDocument = new XDocument(mmlContent);
        var originalCulture = CultureInfo.CurrentCulture;
        if (originalCulture.NumberFormat.NumberDecimalSeparator != ".")
        {
            var cultureInfo = (CultureInfo)originalCulture.Clone();
            cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
            CultureInfo.CurrentCulture = cultureInfo;
        }

        try
        {
            var root = mMathDocument.Root;
            if (root == null)
            {
                throw new ArgumentException("Invalid MathML content: Root element is null.");
            }

            var parentNode = new MathNode(root.Name.LocalName,
                root.Attributes()
                    .ToDictionary(kvp => kvp.Name.ToString(),
                        kvp => kvp.Value),
                _mMathConfig, null);
            ParseMml(root, parentNode, 0);
            return parentNode.MakeImage();
        }
        finally
        {
            // 恢复线程原始的区域性设置
            if (originalCulture.NumberFormat.NumberDecimalSeparator != ".")
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }

    private void ParseMml(XElement root, MathNode parentNode, int depth)
    {
        var depth1 = depth + 1;
        foreach (var element in root.Elements())
        {
            var mn = new MathNode(element.Name.LocalName,
                element.Attributes().ToDictionary((Func<XAttribute, string>)(kvp => kvp.Name.ToString()),
                    (Func<XAttribute, string>)(kvp => kvp.Value)), this._mMathConfig, parentNode);
            element.Nodes().Where(x => x.NodeType is XmlNodeType.Text or XmlNodeType.Whitespace).ToList()
                .ForEach((Action<XNode>)(x => mn.Text += string.Join(" ", ((XText)x).Value.Split(null))));
            this.ParseMml(element, mn, depth1);
        }
    }
}