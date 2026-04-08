using LLMClient.Abstraction;
using Newtonsoft.Json;

namespace LLMClient.Data;

public class ParameterizedLLMModelPO : LLMModelPersistModel
{
    [JsonProperty("Params")]
    public IModelParams? Parameters { get; set; }
}