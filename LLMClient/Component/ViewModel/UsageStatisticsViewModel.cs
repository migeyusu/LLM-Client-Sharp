using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using SkiaSharp;

namespace LLMClient.Component.ViewModel;

public class PilotInfo<T>
{
    public PilotInfo(string name, T value, SolidColorPaint paint, int index)
    {
        Name = name;
        Paint = paint;
        Data = value;
        Index = index;
    }

    public string Name { get; set; }

    public T Data { get; set; }

    public SolidColorPaint Paint { get; set; }

    public int Index { get; set; }
}

public class UsageStatisticsViewModel : BaseViewModel
{
    private readonly IEndpointService _endpointService;
    private bool _isPieChart;
    private ISeries[]? _averageTpsSeries;
    private List<LegendItem> _legend = [];
    private int _maxItemsCount = 10;

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

    public ISeries[]? AverageTpsSeries
    {
        get => _averageTpsSeries;
        private set
        {
            if (Equals(value, _averageTpsSeries)) return;
            _averageTpsSeries = value;
            OnPropertyChanged();
        }
    }

    public record LegendItem(string Name, string HexColor);

    public List<LegendItem> Legend
    {
        get => _legend;
        private set
        {
            if (Equals(value, _legend)) return;
            _legend = value;
            OnPropertyChanged();
        }
    }

    public int MaxItemsCount
    {
        get => _maxItemsCount;
        set
        {
            if (value == _maxItemsCount) return;
            _maxItemsCount = value;
            OnPropertyChanged();
            if (value < _existingItemsCount)
            {
                UpdateCharts();
            }
        }
    }

    public UsageStatisticsViewModel(IEndpointService endpointService)
    {
        _endpointService = endpointService;
        UpdateCharts();
    }

    public IRelayCommand RefreshCommand => new RelayCommand(UpdateCharts);

    private int _existingItemsCount = 0;

    private void UpdateCharts()
    {
        IList<(string Name, UsageCount Usage)> models = new List<(string Name, UsageCount Usage)>();
        foreach (var endpoint in _endpointService.AvailableEndpoints)
        {
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry != null && model.Telemetry.CallTimes > 0)
                {
                    string name = $"{model.Name} ({endpoint.DisplayName})";
                    models.Add((name, model.Telemetry));
                }
            }
        }

        _existingItemsCount = models.Count;
        models = models.OrderBy((tuple => tuple.Usage.CallTimes))
            .Take(MaxItemsCount)
            .ToArray();
        Legend = models.Select(m =>
        {
            var color = GetModelColor(m.Name);
            return new LegendItem(m.Name, color.ToString());
        }).ToList();

        // Always setup axes for Cartesian charts (Average TPS is always Cartesian)
        // Use RowSeries (Horizontal Bars), so YAxis holds the labels (Categories) and XAxis holds the values

        if (IsPieChart)
        {
            CompletionTokensSeries = CreatePieSeries(models, m => m.Usage.CompletionTokens);
            CallTimesSeries = CreatePieSeries(models, m => m.Usage.CallTimes);
            PriceSeries = CreatePieSeries(models, m => m.Usage.Price, "C2");
        }
        else
        {
            CompletionTokensSeries =
                CreateRowSeries(models, (m, i) => { return new Coordinate(i, m.Data.CompletionTokens); });
            CallTimesSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.CallTimes));
            PriceSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.Price));
            AverageTpsSeries ??= CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.AverageTps));
        }

        OnPropertyChanged(nameof(CompletionTokensSeries));
        OnPropertyChanged(nameof(CallTimesSeries));
        OnPropertyChanged(nameof(PriceSeries));
    }

    private ISeries[] CreateRowSeries(IList<(string Name, UsageCount Usage)> models,
        Func<PilotInfo<UsageCount>, int, Coordinate> mapping, string format = "N0")
    {
        return models.Select((m, index) =>
        {
            var color = GetModelColor(m.Name);
            var paint = new SolidColorPaint(color);

            return new RowSeries<PilotInfo<UsageCount>>()
            {
                Name = m.Name,
                Values =
                [
                    new PilotInfo<UsageCount>(
                        m.Name,
                        m.Usage,
                        paint,
                        index)
                ],
                Fill = paint,
                DataLabelsFormatter = point =>
                {
                    var val = point.Coordinate.PrimaryValue;
                    return val.ToString(format);
                },
                DataLabelsPosition = DataLabelsPosition.End,
                Mapping = mapping,
                ShowDataLabels = true,
                Padding = 10,
                MaxBarWidth = 50,
                DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                DataLabelsTranslate = new LvcPoint(-1, 0),
            };
        }).Cast<ISeries>().ToArray();
    }

    private ISeries[] CreatePieSeries<T>(IList<(string Name, UsageCount Usage)> models,
        Func<(string Name, UsageCount Usage), T> valueSelector, string format = "N0")
    {
        var total = models.Sum(m => Convert.ToDouble(valueSelector(m)));
        if (total <= 0) return [];

        return models.Select(m =>
        {
            var color = GetModelColor(m.Name);
            return new PieSeries<double>
            {
                Values = new[] { Convert.ToDouble(valueSelector(m)) },
                Name = m.Name,
                Fill = new SolidColorPaint(color),
                ShowDataLabels = false,
            };
        }).Cast<ISeries>().ToArray();
    }

    private SKColor GetModelColor(string name)
    {
        return SKColor.FromHsl((uint)(new Random(name.GetHashCode()).NextDouble() * 360), 70, 50);
    }
}