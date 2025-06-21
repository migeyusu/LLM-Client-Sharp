using LLMClient.Abstraction;
using Microsoft.Extensions.AI;

public class ImagePriceCalculator : IPriceCalculator
{
    public double Calculate(UsageDetails usage)
    {
        return Double.NaN;
    }
}

public class TokenBasedPriceCalculator : IPriceCalculator
{
    /// <summary>
    /// input price per 1m tokens
    /// </summary>
    public double InputPrice { get; set; }

    /// <summary>
    /// output price per 1m tokens
    /// </summary>
    public double OutputPrice { get; set; }

    /// <summary>
    /// discount factor, 1 means no discount, 0 means free
    /// </summary>
    public double DiscountFactor { get; set; } = 1;

    public bool Enable { get; set; } = true;

    public double Calculate(UsageDetails usage)
    {
        if (DiscountFactor == 0d || usage.InputTokenCount == null || usage.OutputTokenCount == null)
        {
            return 0f;
        }

        return (InputPrice * usage.InputTokenCount.Value / 1000000 +
                OutputPrice * usage.OutputTokenCount.Value / 1000000) *
               DiscountFactor;
    }
}