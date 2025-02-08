using System.Text.Json.Serialization;
using LLMClient.Azure.Models;

namespace LLMClient;

[JsonDerivedType(typeof(AzureJsonModel), "azure")]
public interface IModelParams
{
}