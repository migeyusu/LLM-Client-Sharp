using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using Markdig.Extensions.CustomContainers;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;

namespace LLMClient.UI.Render;

public class ThinkBlockRenderer : WpfObjectRenderer<CustomContainer>
{
    public static ComponentResourceKey ThinkBlockToggleStyleKey { get; } =
        new(typeof(ThinkBlockRenderer), (object)nameof(ThinkBlockToggleStyleKey));

    public static ComponentResourceKey ThinkBlockSectionStyleKey { get; } =
        new(typeof(ThinkBlockRenderer), (object)nameof(ThinkBlockSectionStyleKey));

    protected override void Write(WpfRenderer renderer, CustomContainer obj)
    {
        if (obj.Info != "think")
        {
            renderer.WriteChildren(obj);
            return;
        }

        var blockUiContainer = new BlockUIContainer();
        renderer.Push(blockUiContainer);
        var toggleButton = new ToggleButton() { IsChecked = true };
        toggleButton.SetResourceReference(FrameworkElement.StyleProperty, ThinkBlockToggleStyleKey);
        renderer.Push(toggleButton);
        renderer.Pop();
        renderer.Pop();
        var section = new Section();
        section.SetResourceReference(FrameworkElement.StyleProperty, ThinkBlockSectionStyleKey);
        toggleButton.Checked += (s, e) =>
        {
            if (((ToggleButton)s).Tag is Block[] blocks)
            {
                section.Blocks.AddRange(blocks);
            }
            
            section.BorderThickness = new Thickness(1, 0, 1, 1);
        };
        toggleButton.Unchecked += (s, e) =>
        {
            section.Blocks.Clear();
            section.BorderThickness = new Thickness(0);
        };
        renderer.Push(section);
        renderer.WriteChildren(obj);
        toggleButton.Tag = section.Blocks.ToArray();
        renderer.Pop();
    }
}