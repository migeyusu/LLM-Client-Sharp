using System.Text.Json.Serialization;
using LLMClient.Endpoints;
using Microsoft.Extensions.AI;

namespace LLMClient.Abstraction;

[JsonDerivedType(typeof(TokenBasedPriceCalculator), "TokenBasedPriceCalculator")]
public interface IPriceCalculator
{
    double Calculate(UsageDetails usage);
}