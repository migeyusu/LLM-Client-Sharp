using System.Xml.Linq;
using LatexToMathML;
using SvgMath;

namespace LLMClient.Test;

public class Latex
{
    /*[Fact]
    public void Test1()
    {
        var options = new PngMathRendererOptions
        {
            Resolution = 150,
            Preamble = @"\usepackage{amsmath}
                    \usepackage{amsfonts}
                    \usepackage{amssymb}
                    \usepackage{color}",
            // Specify the scaling factor 300%.
            Scale = 3000,
            // Specify the foreground color.
            TextColor = System.Drawing.Color.Black,
            // Specify the background color.
            BackgroundColor = System.Drawing.Color.White,
            // Specify whether to show the terminal output on the console or not.
            ShowTerminal = false
        };
// Create the output stream for the formula image.
        using (Stream stream = File.Open(@"D:\math-formula.png", FileMode.Create))
            // Run rendering.
        {
            var pngMathRenderer = new PngMathRenderer();
            pngMathRenderer.Render(@"\boxed{ Rf(\theta, s) = \int_{L_{\theta,s}} f(x, y) \, ds }", stream, options);
        }
    }*/

    [Fact]
    public void Convet()
    {
        String begin = @"\begin{document} $";
        String end = @"$ \end{document}";
        var formula = begin + @"R(f)(\theta, s) = \int_{L(\theta, s)} f(x, y) \, dl" + end;
        var parser = new LatexParser(formula, new LatexMathToMathMLConverter());
        var root = parser.Root;
        var convert = root.Convert();
        var xElement = XElement.Parse(convert);
        Mml m = new Mml(xElement,Path.GetFullPath("svgmath.xml"));
        m.MakeSvg().Save("test.svg");
    }
}