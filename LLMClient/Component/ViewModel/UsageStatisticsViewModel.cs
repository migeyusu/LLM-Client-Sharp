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

    public bool IsPieChart
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            UpdateSource();
        }
    }

    public long TotalCompletionTokens
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] CompletionTokensSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public int TotalCallTimes
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] CallTimesSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public double TotalPrice
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] PriceSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public int TotalErrorTimes
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[] ErrorTimesSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public float MeanAvgTps
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public float MeanAvgLatencyPerTokens
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[]? AverageTpsSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ISeries[]? AvgLatencyPerTokensSeries
    {
        get;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public record LegendItem(string Name, string HexColor);

    public List<LegendItem> Legend
    {
        get => field;
        private set
        {
            if (Equals(value, field)) return;
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public int SortCriteriaIndex
    {
        get => field;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            UpdateSource();
        }
    } = 0;

    public int MaxItemsCount
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
            if (value <= _existingItemsCount)
            {
                UpdateSource();
            }
        }
    } = 10;

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

    private void UpdateStatistics(IList<(string Name, UsageCounter Usage)> models)
    {
        _existingItemsCount = models.Count;
        models = (SortCriteriaIndex == 0 
                ? models.OrderByDescending(tuple => tuple.Usage.CallTimes) 
                : models.OrderByDescending(tuple => tuple.Usage.CompletionTokens))
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
        TotalErrorTimes = models.Sum(m => m.Usage.ErrorTimes);
        MeanAvgTps = models.Count > 0
            ? models.Average(m => m.Usage.AverageTps)
            : 0;
        MeanAvgLatencyPerTokens = models.Count > 0
            ? models.Average(m => m.Usage.AvgLatencyPerTokens)
            : 0;

        if (IsPieChart)
        {
            CompletionTokensSeries = CreatePieSeries(models, m => m.Usage.CompletionTokens);
            CallTimesSeries = CreatePieSeries(models, m => m.Usage.CallTimes);
            PriceSeries = CreatePieSeries(models, m => m.Usage.Price, "C2");
            ErrorTimesSeries = CreatePieSeries(models, m => m.Usage.ErrorTimes);
        }
        else
        {
            CompletionTokensSeries =
                CreateRowSeries(models, (m, i) => { return new Coordinate(i, m.Data.CompletionTokens); });
            CallTimesSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.CallTimes));
            PriceSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.Price));
            ErrorTimesSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.ErrorTimes));
        }

        AverageTpsSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.AverageTps));
        AvgLatencyPerTokensSeries = CreateRowSeries(models, (m, i) => new Coordinate(i, m.Data.AvgLatencyPerTokens), "N2");
    }

    /// <summary>
    /// 按系列统计的使用数据
    /// </summary>
    private void UpdateSeriesStatistics()
    {
        IList<(string Name, UsageCounter Usage)> series = new List<(string Name, UsageCounter Usage)>();
        foreach (var endpoint in _endpointService.AllEndpoints)
        {
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry is { CallTimes: > 0 })
                {
                    var name = model.SeriesName ?? model.Name + $" ({endpoint.DisplayName})";
                    var existing = series.FirstOrDefault(s => s.Name == name);
                    if (existing.Name != null)
                    {
                        existing.Usage.Add(model.Telemetry);
                    }
                    else
                    {
                        var usage = new UsageCounter(model.Telemetry);
                        series.Add((name, usage));
                    }
                }
            }
        }

        // Archived (deleted) model/endpoint telemetry – grouped by the same name key used above
        foreach (var archived in _endpointService.ArchivedTelemetry)
        {
            if (archived.Telemetry.CallTimes <= 0) continue;

            var name = $"{archived.ModelName} ({archived.EndpointDisplayName})";
            var existing = series.FirstOrDefault(s => s.Name == name);
            if (existing.Name != null)
            {
                existing.Usage.Add(archived.Telemetry);
            }
            else
            {
                series.Add((name, new UsageCounter(archived.Telemetry)));
            }
        }

        UpdateStatistics(series);
    }

    /// <summary>
    /// 按端点统计的使用数据
    /// </summary>
    private void UpdateEndpointStatistics()
    {
        var endpointMap = new Dictionary<string, (string DisplayName, UsageCounter Usage)>();

        // Current (live) endpoints
        foreach (var endpoint in _endpointService.AllEndpoints)
        {
            UsageCounter? usage = null;
            foreach (var model in endpoint.AvailableModels)
            {
                if (model.Telemetry != null && model.Telemetry.CallTimes > 0)
                {
                    if (usage == null)
                    {
                        usage = new UsageCounter(model.Telemetry);
                    }
                    else
                    {
                        usage.Add(model.Telemetry);
                    }
                }
            }

            if (usage?.CallTimes > 0)
            {
                endpointMap[endpoint.Name] = (endpoint.DisplayName, usage);
            }
        }

        // Archived (deleted) model/endpoint telemetry
        foreach (var archived in _endpointService.ArchivedTelemetry)
        {
            if (archived.Telemetry.CallTimes <= 0) continue;

            if (endpointMap.TryGetValue(archived.EndpointName, out var entry))
            {
                entry.Usage.Add(archived.Telemetry);
            }
            else
            {
                endpointMap[archived.EndpointName] =
                    (archived.EndpointDisplayName, new UsageCounter(archived.Telemetry));
            }
        }

        var endpoints = endpointMap.Values
            .Select(e => (e.DisplayName, e.Usage))
            .ToList<(string Name, UsageCounter Usage)>();

        UpdateStatistics(endpoints);
    }

    /// <summary>
    /// 按模型统计的使用数据
    /// </summary>
    private void UpdateModelsStatistics()
    {
        IList<(string Name, UsageCounter Usage)> models = new List<(string Name, UsageCounter Usage)>();
        foreach (var endpoint in _endpointService.AllEndpoints)
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

        // Include telemetry from deleted models/endpoints
        foreach (var archived in _endpointService.ArchivedTelemetry)
        {
            if (archived.Telemetry.CallTimes > 0)
            {
                var name = $"{archived.ModelName} ({archived.EndpointDisplayName})";
                models.Add((name, archived.Telemetry));
            }
        }

        UpdateStatistics(models);
    }

    private ISeries[] CreateRowSeries(IList<(string Name, UsageCounter Usage)> models,
        Func<PilotInfo<UsageCounter>, int, Coordinate> mapping, string format = "N0")
    {
        return models.Select((m, index) =>
        {
            var color = GetModelColor(m.Name);
            var paint = new SolidColorPaint(color);

            return new RowSeries<PilotInfo<UsageCounter>>()
            {
                Name = m.Name,
                Values =
                [
                    new PilotInfo<UsageCounter>(
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

    private ISeries[] CreatePieSeries<T>(IList<(string Name, UsageCounter Usage)> models,
        Func<(string Name, UsageCounter Usage), T> valueSelector, string format = "N0")
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