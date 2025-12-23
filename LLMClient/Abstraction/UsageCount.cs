using LLMClient.Component.ViewModel.Base;
using LLMClient.Endpoints;

namespace LLMClient.Abstraction;

public class UsageCount : BaseViewModel
{
    private long _completionTokens;
    private int _callTimes;
    private double _price;
    private float _averageTps;

    public long CompletionTokens
    {
        get => _completionTokens;
        set
        {
            if (value == _completionTokens) return;
            _completionTokens = value;
            OnPropertyChanged();
        }
    }

    public int CallTimes
    {
        get => _callTimes;
        set
        {
            if (value == _callTimes) return;
            _callTimes = value;
            OnPropertyChanged();
        }
    }

    public double Price
    {
        get => _price;
        set
        {
            if (value.Equals(_price)) return;
            _price = value;
            OnPropertyChanged();
        }
    }

    public float AverageTps
    {
        get => _averageTps;
        set
        {
            if (value.Equals(_averageTps)) return;
            _averageTps = value;
            OnPropertyChanged();
        }
    }

    public UsageCount()
    {
    }

    public UsageCount(CompletedResult result)
    {
        this.CallTimes = 1;
        this.CompletionTokens = result.Usage?.OutputTokenCount ?? 0;
        this.Price = result.Price ?? 0d;
        var tps = result.CalculateTps();
        if (float.IsNaN(tps) || float.IsInfinity(tps) || tps <= 0)
        {
            return;
        }
        this.AverageTps = tps;
    }

    public void Add(CompletedResult result)
    {
        this.CallTimes++;
        this.CompletionTokens += result.Usage?.OutputTokenCount ?? 0;
        this.Price += result.Price ?? 0d;
        var tps = result.CalculateTps();
        if (float.IsNaN(tps) || float.IsInfinity(tps) || tps <= 0)
        {
            return;
        }

        this.AverageTps = (this.AverageTps + tps) / 2f;
    }
}