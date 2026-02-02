using System.Windows.Data;
using System.Windows.Media;
using DocumentFormat.OpenXml.Drawing.Charts;
using LambdaConverters;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog;

internal static class NavigationConverters
{
    public static readonly IValueConverter RoleToBackground = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(220, 240, 255)); // 浅蓝
        }

        if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(240, 255, 240)); // 浅绿
        }

        if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(255, 250, 230)); // 浅黄
        }

        return Brushes.White;
    });

    public static readonly IValueConverter RoleToBorder = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(100, 160, 220));
        }
        else if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(100, 180, 100));
        }
        else if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(200, 180, 100));
        }

        return Brushes.Gray;
    });

    public static readonly IValueConverter RoleToForeground = ValueConverter.Create<ChatRole, Brush>(e =>
    {
        if (e.Value == ChatRole.User)
        {
            return new SolidColorBrush(Color.FromRgb(30, 80, 140));
        }
        else if (e.Value == ChatRole.Assistant)
        {
            return new SolidColorBrush(Color.FromRgb(30, 120, 50));
        }
        else if (e.Value == ChatRole.System)
        {
            return new SolidColorBrush(Color.FromRgb(140, 120, 40));
        }

        return Brushes.Black;
    });

    public static readonly IValueConverter SubString = ValueConverter.Create<string, string, int>(args =>
    {
        var argsValue = args.Value;
        if (!string.IsNullOrEmpty(argsValue))
        {
            return argsValue.Length > args.Parameter ? argsValue[args.Parameter..] : argsValue;
        }
        return argsValue;
    });
}