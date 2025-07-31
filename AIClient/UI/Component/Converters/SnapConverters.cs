using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using LambdaConverters;
using LLMClient.UI.Project;

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
        ValueConverter.Create<Thickness, double>(e => e.Value.Left);

    public static readonly IValueConverter ProjectTaskStatusToBrush =
        ValueConverter.Create<ProjectTaskStatus, Brush>(args =>
        {
            switch (args.Value)
            {
                case ProjectTaskStatus.InProgress:
                    return Brushes.IndianRed;
                case ProjectTaskStatus.Completed:
                    return Brushes.Green;
                case ProjectTaskStatus.RolledBack:
                    return Brushes.Gray;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        });

    public static readonly IValueConverter EnumToDescriptionConverter =
        ValueConverter.Create<Enum, string>(e => e.Value.GetEnumDescription());

    private static readonly JsonSerializerOptions ObjectSerializeOptions =
        new JsonSerializerOptions() { WriteIndented = true };

    public static readonly IValueConverter ObjectToJsonConverter =
        ValueConverter.Create<object?, string>(e => e.Value != null
            ? JsonSerializer.Serialize(e.Value, Extension.DefaultJsonSerializerOptions)
            : string.Empty);

    public static readonly IValueConverter EnumerableToVisibilityConverter =
        ValueConverter.Create<IEnumerable<object>?, Visibility>(e =>
        {
            if (e.Value == null || !e.Value.Any())
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        });

    public static readonly IValueConverter EnumerableToBoolConverter =
        ValueConverter.Create<IEnumerable<object>?, bool>(e =>
        {
            if (e.Value == null || !e.Value.Any())
            {
                return false;
            }

            return true;
        });
}