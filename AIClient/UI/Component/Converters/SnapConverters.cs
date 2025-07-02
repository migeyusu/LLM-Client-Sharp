using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Data;
using LambdaConverters;

namespace LLMClient.UI.Component.Converters;

internal static class SnapConverters
{
    public static readonly IValueConverter TraceToBrush =
        ValueConverter.Create<TraceEventType, Brush>(e =>
        {
            switch (e.Value)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    return Brushes.Red;
                case TraceEventType.Warning:
                    return Brushes.Orange;
                case TraceEventType.Information:
                case TraceEventType.Verbose:
                    return Brushes.Green;
                default:
                    return Brushes.Transparent;
            }
        });

    public static readonly IValueConverter ThicknessToDoubleConverter =
        ValueConverter.Create<Thickness, double>(e =>
        {
            // 只取左边的值
            return e.Value.Left;
        });
}