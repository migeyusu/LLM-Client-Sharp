using Markdig.Renderers;

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
        ObjectRenderers.Add(new ThinkBlockRenderer());
        ObjectRenderers.Add(new LinkInlineRendererEx());
        base.LoadRenderers();
        _isRendererLoaded = true;
    }

    public void Initialize()
    {
        LoadRenderers();
    }
}