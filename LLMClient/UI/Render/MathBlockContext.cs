using LLMClient.UI.Component.CustomControl;
using Markdig.Extensions.Mathematics;

namespace LLMClient.UI.Render;

public class MathBlockContext
{
    public AsyncThemedIcon Image { get; }

    public string Latex { get; }

    private readonly MathBlock _mathBlock;

    public MathBlockContext(MathBlock mathBlock, AsyncThemedIcon image)
    {
        this._mathBlock = mathBlock;
        Image = image;
        Latex = _mathBlock.Lines.ToString();
    }
}