namespace LLMClient.Workflow;

/// <summary>
/// 定义工作流的所有可能状态
/// </summary>
public enum AgentState
{
    Idle,           // 空闲
    Inspecting,     // 分析项目结构/Context Engineering
    Planning,       // 生成计划
    Coding,         // 编写代码 (ToolCall 发生地)
    Reviewing,      // 编译检查/静态分析
    Writing,        // 将变更写入文件系统
    Failed,         // 发生无法恢复的错误
    Completed       // 任务完成
}

public record WorkflowStepLog(DateTime Timestamp, AgentState State, string Message, bool IsSuccess);