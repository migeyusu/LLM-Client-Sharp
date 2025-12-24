using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace LLMClient.Component.Chart;

public partial class UsageStatsTooltip : UserControl, IChartTooltip
{
    public class TooltipItem
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public Brush ColorBrush { get; set; } = Brushes.Gray;
    }

    public ObservableCollection<TooltipItem> Items { get; } = new();

    public FindingStrategy FindingStrategy { get; set; } = FindingStrategy.Automatic;

    public TooltipPosition TooltipPosition { get; set; }

    private readonly ToolTip _toolTip;

    public UsageStatsTooltip()
    {
        InitializeComponent();
        DataContext = this;
        _toolTip = new ToolTip
        {
            Placement = PlacementMode.Mouse,
            StaysOpen = true,
            Content = this,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HasDropShadow = false
        };
    }

    public void Hide(LiveChartsCore.Chart chart)
    {
        _toolTip.IsOpen = false;
    }

    public void Show(IEnumerable<ChartPoint> data, LiveChartsCore.Chart chart)
    {
        var points = data?.ToArray() ?? [];
        Items.Clear();
        if (points.Length == 0)
        {
            _toolTip.IsOpen = false;
            return;
        }

        foreach (var point in points)
        {
            var series = point.Context.Series;
            // Use dynamic to access Fill property if interface is not resolved correctly
            var fill = (dynamic)series;
            var paint = fill.Fill as SolidColorPaint;
            var color = paint?.Color ?? SKColors.Gray;

            Items.Add(new TooltipItem
            {
                Label = series.Name ?? string.Empty,
                Value = point.Coordinate.PrimaryValue.ToString("N2"),
                ColorBrush = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue))
            });
        }

        _toolTip.PlacementTarget = (UIElement)chart.View;
        _toolTip.IsOpen = false;
        _toolTip.IsOpen = true;
    }

    public void Move(IEnumerable<ChartPoint> data, LiveChartsCore.Chart chart)
    {
        // PlacementMode.Mouse keeps following cursor automatically.
    }
}
