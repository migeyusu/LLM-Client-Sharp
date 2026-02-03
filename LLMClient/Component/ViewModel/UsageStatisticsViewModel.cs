using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;
using Microsoft.Web.WebView2.Core.Raw;
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

    public bool IsPieChart
    {
        get => _isPieChart;
        set
        {
            if (value == _isPieChart) return;
            _isPieChart = value;
            OnPropertyChanged();
            UpdateSource();
        }
    }

    public long TotalCompletionTokens
    {
        get => _totalCompletionTokens;
        set
        {
            if (value == _totalCompletionTokens) return;
            _totalCompletionTokens = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] CompletionTokensSeries
    {
        get => _completionTokensSeries;
        private set
        {
            if (Equals(value, _completionTokensSeries)) return;
            _completionTokensSeries = value;
            OnPropertyChanged();
        }
    }

    public int TotalCallTimes
    {
        get => _totalCallTimes;
        set
        {
            if (value == _totalCallTimes) return;
            _totalCallTimes = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] CallTimesSeries
    {
        get => _callTimesSeries;
        private set
        {
            if (Equals(value, _callTimesSeries)) return;
            _callTimesSeries = value;
            OnPropertyChanged();
        }
    }

    public double TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] PriceSeries
    {
        get => _priceSeries;
        private set
        {
            if (Equals(value, _priceSeries)) return;
            _priceSeries = value;
            OnPropertyChanged();
        }
    }

    public float MeanAvgTps
    {
        get => _meanAvgTps;
        set
        {
            if (value.Equals(_meanAvgTps)) return;
            _meanAvgTps = value;
            OnPropertyChanged();
        }
    }

    private ISeries[]? _averageTpsSeries;

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

    private List<LegendItem> _legend = [];

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

    private int _maxItemsCount = 10;

    public int MaxItemsCount
    {
        get => _maxItemsCount;
        set
        {
            if (value == _maxItemsCount) return;
            _maxItemsCount = value;
            OnPropertyChanged();
            if (value <= _existingItemsCount)
            {
                UpdateSource();
            }
        }
    }

    public int CriteriaIndex
    {
        get => _criteriaIndex;
        set
        {
            if (value == _criteriaIndex) return;
            _criteriaIndex = value;
            OnPropertyChanged();
            UpdateSource();
        }
    }

    public IRelayCommand RefreshCommand => new RelayCommand(UpdateSource);

    public UsageStatisticsViewModel(IEndpointService endpointService)
    {
        _endpointService = endpointService;
        UpdateSource();
    }


    private int _existingItemsCount = 0;
    private long _totalCompletionTokens;
    private int _totalCallTimes;
    private double _totalPrice;
    private float _meanAvgTps;
    private ISeries[] _completionTokensSeries = [];
    private ISeries[] _callTimesSeries = [];
    private ISeries[] _priceSeries = [];
    private int _criteriaIndex = 0;

    private void UpdateSource()
    {
        if (_criteriaIndex == 0)
        {
            UpdateModelsStatistics();
        }
        else if (_criteriaIndex == 1)
        {
            UpdateEndpointStatistics();
        }
        else if (_criteriaIndex == 2)
        {
            UpdateSeriesStatistics();
        }
    }

    private void UpdateStatistics(IList<(string Name, UsageCount Usage)> models)
    {
        _existingItemsCount = models.Count;
        models = models.OrderByDescending(tuple => tuple.Usage.CompletionTokens)
            .Take(MaxItemsCount)
            .ToArray();
        Legend = models.Select(m =>
        {
            var color = GetModelColor(m.Name);
            return new LegendItem(m.Name, color.ToString());
        }).ToList();

        // Always setup axes for Cartesian charts (Average TPS is always Cartesian)
        // Use RowSeries (Horizontal Bars), so YAxis holds the labels (Categories) and XAxis holds the values
        TotalCompletionTokens = models.Sum(m => m.Usage.CompletionTokens);
        TotalCallTimes = models.Sum(m => m.Usage.CallTimes);
        TotalPrice = models.Sum(m => m.Usage.Price);
        MeanAvgTps = models.Count > 0
            ? models.Average(m => m.Usage.AverageTps)
            : 0;
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
        }

        AverageTpsSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.AverageTps));
    }

    /// <summary>
    /// 按系列统计的使用数据
    /// </summary>
    private void UpdateSeriesStatistics()
    {
        IList<(string Name, UsageCount Usage)> series = new List<(string Name, UsageCount Usage)>();
        foreach (var endpoint in _endpointService.AvailableEndpoints)
        {
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry != null && model.Telemetry.CallTimes > 0)
                {
                    var name = model.SeriesName ?? model.Name;
                    var existing = series.FirstOrDefault(s => s.Name == name);
                    if (existing.Name != null)
                    {
                        existing.Usage.Add(model.Telemetry);
                    }
                    else
                    {
                        series.Add((name, model.Telemetry));
                    }
                }
            }
        }

        UpdateStatistics(series);
    }

    /// <summary>
    /// 按端点统计的使用数据
    /// </summary>
    private void UpdateEndpointStatistics()
    {
        IList<(string Name, UsageCount Usage)> endpoints = new List<(string Name, UsageCount Usage)>();
        foreach (var endpoint in _endpointService.AvailableEndpoints)
        {
            UsageCount? usage = null;
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry != null && model.Telemetry.CallTimes > 0)
                {
                    if (usage == null)
                    {
                        usage = new UsageCount(model.Telemetry);
                    }
                    else
                    {
                        usage.Add(model.Telemetry);
                    }
                }
            }

            if (usage?.CallTimes > 0)
            {
                endpoints.Add((endpoint.DisplayName, usage));
            }
        }

        UpdateStatistics(endpoints);
    }

    /// <summary>
    /// 按模型统计的使用数据
    /// </summary>
    private void UpdateModelsStatistics()
    {
        IList<(string Name, UsageCount Usage)> models = new List<(string Name, UsageCount Usage)>();
        foreach (var endpoint in _endpointService.AvailableEndpoints)
        {
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry is { CallTimes: > 0 })
                {
                    var name = $"{model.Name} ({endpoint.DisplayName})";
                    models.Add((name, model.Telemetry));
                }
            }
        }

        UpdateStatistics(models);
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