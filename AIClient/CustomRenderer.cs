namespace LLMClient;

public class CustomRenderer : Markdig.Renderers.WpfRenderer
{
    protected override void LoadRenderers()
    {
        ObjectRenderers.Add(new CodeRenderer());
        base.LoadRenderers();
    }
}