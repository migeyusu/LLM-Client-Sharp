using System.Windows.Documents;
using Markdig.Renderers;

namespace LLMClient;

public class CustomRenderer : WpfRenderer
{
    private bool _isRendererLoaded = false;

    protected override void LoadRenderers()
    {
        if (_isRendererLoaded)
        {
            return;
        }

        ObjectRenderers.Add(new CodeRenderer());
        base.LoadRenderers();
        _isRendererLoaded = true;
    }

    public void Initialize()
    {
        LoadRenderers();
    }
}