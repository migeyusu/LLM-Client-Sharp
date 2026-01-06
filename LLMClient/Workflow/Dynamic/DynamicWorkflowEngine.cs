using System.Reflection;
using LLMClient.Workflow.CoreAgents;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient.Workflow.Dynamic;


/// <summary>
/// todo: complete this class to orchestrate the dynamic workflow execution
/// </summary>
public class DynamicWorkflowEngine
{
    private readonly IServiceProvider _sp;
    private readonly WorkflowArchitect _architect;
    private WorkflowContext _context;

    public DynamicWorkflowEngine(IServiceProvider sp, WorkflowArchitect architect)
    {
        _sp = sp;
        _architect = architect;
        _context = new WorkflowContext();
    }

    public async Task RunAsync(string userGoal)
    {
        /*// Phase 1: Planning (LLM 决定路径)
        NotifyUI("Architect is thinking...");
        var blueprint = await _architect.PlanAsync(userGoal);
        
        _context.ExecutionHistory.Add(new WorkflowStepLog(DateTime.Now, AgentState.Planning, $"Plan Generated: {blueprint.GoalSummary}", true));

        // Phase 2: Execution (按图索骥)
        foreach (var step in blueprint.Steps)
        {
            NotifyUI($"Executing Step {step.Id}: {step.AgentName}...");

            // 1. 根据 Name 找到对应的 IAgentStep 实例
            var agentInstance = ResolveAgentByName(step.AgentName);
            
            // 2. 将特定指令注入 Context (临时)
            // 我们可以扩充 Context 增加一个 CurrentInstruction 字段
            _context.CurrentInstruction = step.SpecificInstruction;

            // 3. 执行
            var result = await agentInstance.ExecuteAsync(_context);

            // 4. 处理结果
            if (!result.Success)
            {
                HandleError(result);
                // 这里可以引入高级逻辑：如果失败，让 Architect 重新规划 (Re-Planning)
                break;
            }
            _context.ExecutionHistory.Add(new WorkflowStepLog(DateTime.Now, agentInstance.TargetState, result.OutputMessage, true));
        }
        
        NotifyUI("Workflow Completed.");*/
    }
    
    // 利用 KeyedServices 或者简单的 IEnumerable 查找
    private IAgentStep ResolveAgentByName(string name)
    {
        var agents = _sp.GetServices<IAgentStep>();
        return agents.FirstOrDefault(a => 
                   a.GetType().GetCustomAttribute<AgentCapabilityAttribute>()?.Name == name)
               ?? throw new Exception($"Agent {name} not found");
    }

    private void NotifyUI(string message)
    {
        throw new  NotImplementedException();
    }
}