using System.Text.Json.Serialization;
using LLMClient.Endpoints.Azure.Models;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient;

[JsonDerivedType(typeof(AzureJsonModel), "azure")]
[JsonDerivedType(typeof(DefaultModelParam), "default")]
public interface IModelParams
{
}