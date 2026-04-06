using System;
using Microsoft.Extensions.AI;

namespace LLMClient.Dialog.Models;

public class ContextUsageViewModel
{
    public ContextUsageViewModel(UsageDetails? usageDetails = null, int? maxContextTokens = null)
    {
        UsageDetails = usageDetails;
        MaxContextTokens = maxContextTokens;
    }
    
    /// <summary>
    /// 是否有用量信息
    /// </summary>
    public bool HasUsage => UsageDetails?.InputTokenCount > 0;

    public UsageDetails? UsageDetails { get; }

    public long? ContextUsageTokenCount => UsageDetails?.InputTokenCount;

    public int? MaxContextTokens { get; }

    public bool HasContextUsage => ContextUsageRatio.HasValue;

    public double? ContextUsageRatio
    {
        get
        {
            if (ContextUsageTokenCount is not > 0 || MaxContextTokens is not > 0)
            {
                return null;
            }

            return Math.Clamp(ContextUsageTokenCount.Value / (double)MaxContextTokens.Value, 0d, 1d);
        }
    }

    public double ContextUsagePercent => (ContextUsageRatio ?? 0d) * 100d;

    public bool IsContextUsageWarning
    {
        get
        {
            var ratio = ContextUsageRatio;
            return ratio is >= 0.7 and < 0.9;
        }
    }

    public bool IsContextUsageCritical => ContextUsageRatio >= 0.9;

    public string ContextUsageSummary
    {
        get
        {
            if (ContextUsageTokenCount is null || MaxContextTokens is null)
            {
                return "上下文 --";
            }

            return $"上下文 {FormatTokenCount(ContextUsageTokenCount)} / {FormatTokenCount(MaxContextTokens)} · {ContextUsagePercent:0}%";
        }
    }

    private static string FormatTokenCount(long? value)
    {
        return value switch
        {
            null => "--",
            >= 1000000 => $"{value.Value / 1000000.0:0.##}m",
            >= 1000 => $"{value.Value / 1000.0:0.##}k",
            _ => value.Value.ToString()
        };
    }
}
