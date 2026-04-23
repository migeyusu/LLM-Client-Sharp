using LLMClient.Abstraction;
using LLMClient.Component.ViewModel.Base;

namespace LLMClient.Component.ViewModel;

public class UsageCounter : BaseViewModel
{
    public long CompletionTokens
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public int CallTimes
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public double Price
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public float AverageTps
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public int ErrorTimes
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 每千个token的平均延迟，单位ms
    /// </summary>
    public float AvgLatencyPerTokens
    {
        get;
        set
        {
            if (value.Equals(field)) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public UsageCounter()
    {
    }

    public UsageCounter(CallResult result)
    {
        this.ErrorTimes = result is { IsInterrupt: true, IsCanceled: false } ? 1 : 0;
        this.CallTimes = result.ValidCallTimes;
        this.CompletionTokens = result.Usage?.OutputTokenCount ?? 0;
        this.Price = result.Price ?? 0d;
        var tps = result.CalculateTps();
        if (float.IsNaN(tps) || float.IsInfinity(tps) || tps <= 0)
        {
            return;
        }

        this.AverageTps = tps;
        this.AvgLatencyPerTokens = result.AvgLatencyPerTokens ?? 0f;
    }

    public UsageCounter(UsageCounter other)
    {
        this.ErrorTimes = other.ErrorTimes;
        this.CallTimes = other.CallTimes;
        this.CompletionTokens = other.CompletionTokens;
        this.Price = other.Price;
        this.AverageTps = other.AverageTps;
        this.AvgLatencyPerTokens = other.AvgLatencyPerTokens;
    }

    public void Add(CallResult result)
    {
        this.ErrorTimes += result is { IsInterrupt: true, IsCanceled: false } ? 1 : 0;
        this.CallTimes += result.ValidCallTimes;
        this.CompletionTokens += result.Usage?.OutputTokenCount ?? 0;
        this.Price += result.Price ?? 0d;
        var tps = result.CalculateTps();
        if (float.IsNaN(tps) || float.IsInfinity(tps) || tps <= 0)
        {
            return;
        }

        //加权平均
        this.AverageTps = (this.AverageTps * this.CallTimes + tps * result.ValidCallTimes) /
                          (this.CallTimes + result.ValidCallTimes);
        var latencyPerTokens = result.AvgLatencyPerTokens ?? 0f;
        var avgLatencyPerTokens = this.AvgLatencyPerTokens;
        if (!float.IsNaN(latencyPerTokens) && !float.IsInfinity(latencyPerTokens) && latencyPerTokens > 0
            && !float.IsNaN(avgLatencyPerTokens) && !float.IsInfinity(avgLatencyPerTokens) &&
            avgLatencyPerTokens > 0)
        {
            this.AvgLatencyPerTokens = (avgLatencyPerTokens * this.CompletionTokens +
                                        latencyPerTokens * (result.Usage?.OutputTokenCount ?? 0)) /
                                       (this.CompletionTokens + (result.Usage?.OutputTokenCount ?? 0));
        }
        else if (avgLatencyPerTokens == 0f || float.IsNaN(avgLatencyPerTokens) || float.IsInfinity(avgLatencyPerTokens))
        {
            this.AvgLatencyPerTokens = latencyPerTokens;
        }
    }

    public void Add(UsageCounter other)
    {
        this.ErrorTimes += other.ErrorTimes;
        var otherCallTimes = other.CallTimes;
        this.CallTimes += otherCallTimes;
        this.CompletionTokens += other.CompletionTokens;
        this.Price += other.Price;
        if (float.IsNaN(other.AverageTps) || float.IsInfinity(other.AverageTps) || other.AverageTps <= 0)
        {
            return;
        }

        //加权平均
        this.AverageTps = (this.AverageTps * this.CallTimes + other.AverageTps * otherCallTimes) /
                          (this.CallTimes + otherCallTimes);
        var otherAvgLatencyPerTokens = other.AvgLatencyPerTokens;
        var avgLatencyPerTokens = this.AvgLatencyPerTokens;
        if (!float.IsNaN(otherAvgLatencyPerTokens) && !float.IsInfinity(otherAvgLatencyPerTokens) &&
            otherAvgLatencyPerTokens > 0 && !float.IsNaN(avgLatencyPerTokens) &&
            !float.IsInfinity(avgLatencyPerTokens) && avgLatencyPerTokens > 0)
        {
            this.AvgLatencyPerTokens = (avgLatencyPerTokens * this.CompletionTokens +
                                        otherAvgLatencyPerTokens * other.CompletionTokens) /
                                       (this.CompletionTokens + other.CompletionTokens);
        }
        else if (avgLatencyPerTokens == 0f || float.IsNaN(avgLatencyPerTokens) || float.IsInfinity(avgLatencyPerTokens))
        {
            this.AvgLatencyPerTokens = otherAvgLatencyPerTokens;
        }
    }

    //操作符重载
    public static UsageCounter operator +(UsageCounter a, UsageCounter b)
    {
        var result = new UsageCounter
        {
            CallTimes = a.CallTimes + b.CallTimes,
            CompletionTokens = a.CompletionTokens + b.CompletionTokens,
            Price = a.Price + b.Price
        };

        if (!float.IsNaN(a.AverageTps) && !float.IsInfinity(a.AverageTps) && a.AverageTps > 0 &&
            !float.IsNaN(b.AverageTps) && !float.IsInfinity(b.AverageTps) && b.AverageTps > 0)
        {
            result.AverageTps = (a.AverageTps + b.AverageTps) / 2f;
        }

        return result;
    }
}