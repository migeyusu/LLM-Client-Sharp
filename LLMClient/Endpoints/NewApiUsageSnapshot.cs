namespace LLMClient.Endpoints;

public sealed class NewApiUsageSnapshot
{
    public string QueryUrl { get; set; } = string.Empty;

    public int? TokenId { get; set; }

    public int? UserId { get; set; }

    public string? TokenName { get; set; }

    public string? TokenKey { get; set; }

    public string? GroupName { get; set; }

    public bool? IsEnabled { get; set; }

    public bool UnlimitedQuota { get; set; }

    public string StatusText => IsEnabled switch
    {
        true => "启用",
        false => "禁用",
        null => "未知"
    };

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? AccessedAt { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

    public string CreatedAtText => CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未返回";

    public string AccessedAtText => AccessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "未返回";

    public string ExpiredAtText => ExpiredAt switch
    {
        null => "未返回",
        var value when value.Value <= DateTimeOffset.FromUnixTimeSeconds(0) => "永不过期",
        var value => value.Value.ToString("yyyy-MM-dd HH:mm:ss")
    };

    public string QuotaText => UnlimitedQuota ? "无限制" : Quota.ToString("F2");

    public string QuotaModeText => UnlimitedQuota ? "无限额度" : "固定额度";

    public decimal Quota { get; set; }

    public decimal UsedQuota { get; set; }

    public decimal RemainQuota { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }

    public double UsedPercent => UnlimitedQuota || Quota <= 0
        ? 0
        : Math.Clamp((double)(UsedQuota / Quota * 100m), 0, 100);
}
