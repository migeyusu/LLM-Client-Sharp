using Markdig.Renderers;
using TextMateSharp.Grammars;

namespace LLMClient.Render;

public class CustomRenderer : WpfRenderer
{
    private bool _isRendererLoaded = false;

    protected override void LoadRenderers()
    {
        if (_isRendererLoaded)
        {
            return;
        }
        ObjectRenderers.Add(new TextMateCodeRenderer());
        base.LoadRenderers();
        _isRendererLoaded = true;
    }

    public void Initialize()
    {
        LoadRenderers();
    }
}