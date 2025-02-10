using Markdig.Renderers;
using TextMateSharp.Grammars;

namespace LLMClient.Render;

public class CustomRenderer : WpfRenderer
{
    public ThemeName ThemeName { get; set; } = ThemeName.Light;

    private bool _isRendererLoaded = false;

    protected override void LoadRenderers()
    {
        if (_isRendererLoaded)
        {
            return;
        }

        ObjectRenderers.Add(new CodeRenderer(ThemeName));
        base.LoadRenderers();
        _isRendererLoaded = true;
    }

    public void Initialize()
    {
        LoadRenderers();
    }
}