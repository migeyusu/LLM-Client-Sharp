using System.Text.Json.Serialization;
using LLMClient.Endpoints.Azure.Models;

namespace LLMClient;

[JsonDerivedType(typeof(AzureJsonModel), "azure")]
public interface IModelParams
{
}