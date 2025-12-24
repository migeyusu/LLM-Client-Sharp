using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;

namespace LLMClient.Component.Render;

public class SafeListRender : WpfObjectRenderer<ListBlock>
{
    protected override void Write(WpfRenderer renderer, ListBlock listBlock)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (listBlock == null) throw new ArgumentNullException(nameof(listBlock));

        var list = new List();

        if (listBlock.IsOrdered)
        {
            list.MarkerStyle = TextMarkerStyle.Decimal;

            if (listBlock.OrderedStart != null && (listBlock.DefaultOrderedStart != listBlock.OrderedStart))
            {
                if (int.TryParse(listBlock.OrderedStart, NumberFormatInfo.InvariantInfo, out int parsedStart))
                {
                    // 【修复关键点】：WPF List 在某些情况下不支持 StartIndex <= 0。
                    // 这里做一个 Math.Max(1, ...) 的防御性处理。
                    // 虽然原本是0，但显示为1总比程序崩溃要好。
                    list.StartIndex = Math.Max(1, parsedStart);
                }
            }
        }
        else
        {
            list.MarkerStyle = TextMarkerStyle.Disc;
        }

        renderer.Push(list);

        foreach (var item in listBlock)
        {
            var listItemBlock = (ListItemBlock)item;
            var listItem = new ListItem();
            renderer.Push(listItem);
            renderer.WriteChildren(listItemBlock);
            renderer.Pop();
        }

        renderer.Pop();
    }
}