namespace LLMClient.Workflow.Dynamic;

public class WorkflowBlueprint
{
    // 对本次任务的简要描述
    public string GoalSummary { get; set; }
    
    // 动态生成的步骤列表
    public List<BlueprintStep> Steps { get; set; } = new();
}

public class BlueprintStep
{
    public int Id { get; set; }
    
    // 对应 Attribute 中的 Name
    public string AgentName { get; set; } 
    
    // 给这个 Agent 的具体指令 (Context Engineering 的一部分)
    // 比如：Architect 告诉 RagAgent "去查一下 WPF DataGrid 的用法"
    public string SpecificInstruction { get; set; } 
}