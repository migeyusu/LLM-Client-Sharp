using System.Reflection;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace LLMClient.UI.Component.CustomControl;

public class PopupBoxEx : PopupBox
{
    static PopupBoxEx()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupBoxEx), new FrameworkPropertyMetadata(typeof(PopupBoxEx)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        var popup =
            typeof(PopupBox).GetField("_popup", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(this) as PopupEx;
        if (popup != null)
        {
            foreach (CommandBinding binding in this.CommandBindings)
            {
                popup.CommandBindings.Add(binding);
            }
        }
    }
}