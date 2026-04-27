using System.Text.Json.Serialization;
using LLMClient.Dialog;

namespace LLMClient.Persistence;

[JsonDerivedType(typeof(MiniSweAgentPersistModel), "miniSweAgent")]
[JsonDerivedType(typeof(InspectAgentPersistModel), "inspectAgent")]
[JsonDerivedType(typeof(PlannerAgentPersistModel), "plannerAgent")]
[JsonDerivedType(typeof(SummaryAgentPersistModel), "summaryAgent")]
[JsonDerivedType(typeof(NvidiaResearchClientPersistModel), "nvidiaResearchClient")]
public abstract class AgentPersistModel
{
}

public class MiniSweAgentPersistModel : AgentPersistModel
{
    public int CallCount { get; set; }
    
    public AgentConfig? AgentConfig { get; set; }
    public ParameterizedLLMModelPO? ChatClient { get; set; }
    public Dictionary<string, object?> ExtraTemplateVars { get; set; } = [];
}

public class InspectAgentPersistModel : AgentPersistModel
{
    public int CallCount { get; set; }

    public AgentConfig? AgentConfig { get; set; }

    public ParameterizedLLMModelPO? ChatClient { get; set; }
}

public class PlannerAgentPersistModel : AgentPersistModel
{
    public int CallCount { get; set; }

    public AgentConfig? AgentConfig { get; set; }

    public ParameterizedLLMModelPO? ChatClient { get; set; }
}

public class NvidiaResearchClientPersistModel : AgentPersistModel
{
    public int MaxTopics { get; set; } = 5;
    
    public int MaxSearchPhrases { get; set; } = 3;
    
    public ParameterizedLLMModelPO? PromptModel { get; set; }
    
    public ParameterizedLLMModelPO? ReportModel { get; set; }
}

public class SummaryAgentPersistModel : AgentPersistModel
{
    public ParameterizedLLMModelPO? ChatClient { get; set; }
}

