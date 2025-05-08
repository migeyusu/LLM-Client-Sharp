using System.Text.Json.Serialization;
using LLMClient.Endpoints.OpenAIAPI;

namespace LLMClient;

[JsonDerivedType(typeof(DefaultModelParam), "default")]
public interface IModelParams
{
}