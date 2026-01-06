namespace LLMClient.Workflow.Dynamic;

[AttributeUsage(AttributeTargets.Class)]
public class AgentCapabilityAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    // 可以增加 InputType/OutputType 约束，让 LLM 做简单的类型检查
    
    public AgentCapabilityAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}