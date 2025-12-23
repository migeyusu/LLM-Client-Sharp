using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using SkiaSharp;

namespace LLMClient.Component.ViewModel;

public class UsageStatisticsViewModel : BaseViewModel
{
    private readonly IEndpointService _endpointService;
    private bool _isPieChart;

    public bool IsPieChart
    {
        get => _isPieChart;
        set
        {
            if (value == _isPieChart) return;
            _isPieChart = value;
            OnPropertyChanged();
            UpdateCharts();
        }
    }

    public ISeries[] CompletionTokensSeries { get; private set; } = [];
    public ISeries[] CallTimesSeries { get; private set; } = [];
    public ISeries[] PriceSeries { get; private set; } = [];
    public ISeries[] AverageTpsSeries { get; private set; } = [];

    public Axis[] XAxes { get; private set; } = [];
    public Axis[] YAxes { get; private set; } = [];

    public UsageStatisticsViewModel(IEndpointService endpointService)
    {
        _endpointService = endpointService;
        UpdateCharts();
    }

    public IRelayCommand RefreshCommand => new RelayCommand(UpdateCharts);

    private void UpdateCharts()
    {
        var models = new List<(string Name, UsageCount Usage)>();

        foreach (var endpoint in _endpointService.AvailableEndpoints)
        {
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry != null && model.Telemetry.CallTimes > 0)
                {
                    string name = $"{model.Name} ({endpoint.DisplayName})";
                    models.Add((FormatName(name), model.Telemetry));
                }
            }
        }

        // Always setup axes for Cartesian charts (Average TPS is always Cartesian)
        // Use RowSeries (Horizontal Bars), so YAxis holds the labels (Categories) and XAxis holds the values
        YAxes =
        [
            new Axis
            {
                Labels = models.Select(x => x.Name).ToList(),
                LabelsRotation = 0,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 11
            }
        ];
        XAxes = [new Axis()];

        if (IsPieChart)
        {
            CompletionTokensSeries = CreatePieSeries(models, m => m.Usage.CompletionTokens);
            CallTimesSeries = CreatePieSeries(models, m => m.Usage.CallTimes);
            PriceSeries = CreatePieSeries(models, m => m.Usage.Price, "C2");
            // Average TPS always uses RowSeries because Pie chart doesn't make sense for rates
            AverageTpsSeries = CreateRowSeries(models, m => m.Usage.AverageTps);
        }
        else
        {
            CompletionTokensSeries = CreateRowSeries(models, m => m.Usage.CompletionTokens);
            CallTimesSeries = CreateRowSeries(models, m => m.Usage.CallTimes);
            PriceSeries = CreateRowSeries(models, m => m.Usage.Price, "C2");
            AverageTpsSeries = CreateRowSeries(models, m => m.Usage.AverageTps);
        }

        OnPropertyChanged(nameof(CompletionTokensSeries));
        OnPropertyChanged(nameof(CallTimesSeries));
        OnPropertyChanged(nameof(PriceSeries));
        OnPropertyChanged(nameof(AverageTpsSeries));
        OnPropertyChanged(nameof(XAxes));
        OnPropertyChanged(nameof(YAxes));
    }

    private ISeries[] CreateRowSeries<T>(List<(string Name, UsageCount Usage)> models,
        Func<(string Name, UsageCount Usage), T> valueSelector, string format = "N0")
    {
        var values = models.Select(m => Convert.ToDouble(valueSelector(m))).ToList();

        return
        [
            new RowSeries<double>
            {
                Values = values,
                Name = "Usage",
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue.ToString(format)}",
                MaxBarWidth = 30
            }
        ];
    }

    private ISeries[] CreatePieSeries<T>(List<(string Name, UsageCount Usage)> models,
        Func<(string Name, UsageCount Usage), T> valueSelector, string format = "N0")
    {
        var total = models.Sum(m => Convert.ToDouble(valueSelector(m)));
        if (total <= 0) return [];

        return models.Select(m => new PieSeries<double>
        {
            Values = new[] { Convert.ToDouble(valueSelector(m)) },
            Name = m.Name,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
            DataLabelsFormatter = point =>
            {
                var val = point.Coordinate.PrimaryValue;
                var percentage = total > 0 ? val / total : 0;
                return $"{point.Context.Series.Name}: {val.ToString(format)} ({percentage:P1})";
            },
            ToolTipLabelFormatter = point =>
            {
                var val = point.Coordinate.PrimaryValue;
                return $"{point.Context.Series.Name}: {val.ToString(format)}";
            }
        }).Cast<ISeries>().ToArray();
    }

    private static string FormatName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        if (name.Length <= 10) return name;

        var sb = new System.Text.StringBuilder();
        int count = 0;
        foreach (var c in name)
        {
            sb.Append(c);
            count++;
            if (count >= 10 && (char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.'))
            {
                sb.Append(Environment.NewLine);
                count = 0;
            }
            else if (count >= 15)
            {
                sb.Append(Environment.NewLine);
                count = 0;
            }
        }
        return sb.ToString();
    }
}

