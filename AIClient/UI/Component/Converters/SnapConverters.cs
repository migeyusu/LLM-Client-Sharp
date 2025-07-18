﻿using System.Diagnostics;
using System.Drawing;
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
}