using System.Text.Json.Serialization;
using LLMClient.Agent.MiniSWE;
using LLMClient.Dialog;

namespace LLMClient.Data;

[JsonDerivedType(typeof(MiniSweAgentPersistModel), "miniSweAgent")]
public abstract class AgentPersistModel
{
}

public class MiniSweAgentPersistModel : AgentPersistModel
{
    public int CallCount { get; set; }
    
    public AgentOption? AgentOption { get; set; }
    public ParameterizedLLMModelPO? ChatClient { get; set; }
    public Dictionary<string, object?> ExtraTemplateVars { get; set; } = [];
}

