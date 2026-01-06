using Microsoft.Extensions.DependencyInjection;
using Stateless;

namespace LLMClient.Workflow;

public class WorkflowOrchestrator
{
    private readonly StateMachine<AgentState, AgentTrigger> _machine;
    private readonly IServiceProvider _serviceProvider;
    private WorkflowContext _context;
    
    // UI 通知的事件
    public event Action<AgentState>? StateChanged;
    public event Action<string>? LogUpdated;

    public WorkflowOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _context = new WorkflowContext(); // 默认初始化，也可以 Load

        // 初始化状态机
        _machine = new StateMachine<AgentState, AgentTrigger>(() => _context.ExecutionHistory.LastOrDefault()?.State ?? AgentState.Idle, s => { /* 这里通常不需要 Setter，因为我们只是读状态 */ });

        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        // --- 1. IDLE ---
        _machine.Configure(AgentState.Idle)
            .Permit(AgentTrigger.Start, AgentState.Inspecting);

        // --- 2. INSPECTING (Context Engineering) ---
        _machine.Configure(AgentState.Inspecting)
            .OnEntryAsync(ExecuteCurrentStepAsync) // 进入状态自动执行
            .Permit(AgentTrigger.InspectComplete, AgentState.Planning)
            .Permit(AgentTrigger.CriticalError, AgentState.Failed);

        // --- 3. PLANNING ---
        _machine.Configure(AgentState.Planning)
            .OnEntryAsync(ExecuteCurrentStepAsync)
            .Permit(AgentTrigger.PlanApproved, AgentState.Coding)
            .Permit(AgentTrigger.CriticalError, AgentState.Failed);

        // --- 4. CODING (The Core) ---
        _machine.Configure(AgentState.Coding)
            .OnEntryAsync(ExecuteCurrentStepAsync)
            .Permit(AgentTrigger.CodeGenerated, AgentState.Reviewing)
            .Permit(AgentTrigger.CriticalError, AgentState.Failed);

        // --- 5. REVIEWING (Loop Check) ---
        // 关键：这里体现了循环
        _machine.Configure(AgentState.Reviewing)
            .OnEntryAsync(ExecuteCurrentStepAsync)
            .Permit(AgentTrigger.CodeApproved, AgentState.Writing) // 通过 -> 写入
            .Permit(AgentTrigger.CodeRejected, AgentState.Coding)    // 拒绝 -> 重写 (Loop)
            .Permit(AgentTrigger.CriticalError, AgentState.Failed);

        // --- 6. WRITING ---
        _machine.Configure(AgentState.Writing)
            .OnEntryAsync(ExecuteCurrentStepAsync)
            .Permit(AgentTrigger.WriteComplete, AgentState.Completed)
            .Permit(AgentTrigger.CriticalError, AgentState.Failed);

        // --- Global Config ---
        _machine.OnTransitioned(t => 
        {
            // 记录日志
            LogUpdated?.Invoke($"Transition: {t.Source} -> {t.Destination} via {t.Trigger}");
            StateChanged?.Invoke(t.Destination);
            
            // todo: 可以在这里触发自动持久化
            // SaveContextFunc(_context); 
        });
    }

    /// <summary>
    /// 核心驱动引擎：查找当前状态对应的 Agent 并执行
    /// </summary>
    private async Task ExecuteCurrentStepAsync()
    {
        var currentState = _machine.State;
        
        // 从 DI 容器中获取对应的 Worker
        // 建议注册为 KeyedService 或通过 IEnumerable<IAgentStep> 查找
        var steps = _serviceProvider.GetServices<IAgentStep>();
        var worker = steps.FirstOrDefault(s => s.TargetState == currentState);

        if (worker == null)
        {
            // 如果没有Worker（比如Idle状态），则什么都不做等待外部Trigger
            return;
        }

        try
        {
            LogUpdated?.Invoke($"[Agent] Starting {currentState}...");
            
            // 执行具体的 Agent 逻辑
            var result = await worker.ExecuteAsync(_context);

            // 记录历史
            _context.ExecutionHistory.Add(new WorkflowStepLog(DateTime.Now, currentState, result.OutputMessage, result.Success));

            // 根据 Agent 的决策触发状态机
            // 注意：这里需要异步触发，FireAsync 是最好的
            await _machine.FireAsync(result.NextTrigger);
        }
        catch (Exception ex)
        {
            _context.LastErrorMessage = ex.Message;
            _context.ExecutionHistory.Add(new WorkflowStepLog(DateTime.Now, currentState, ex.Message, false));
            await _machine.FireAsync(AgentTrigger.CriticalError);
        }
    }

    // 暴露给外部 UI 开始的方法
    public async Task StartWorkflowAsync(string prompt)
    {
        _context.UserPrompt = prompt;
        _context.CancellationToken = new CancellationTokenSource().Token;
        await _machine.FireAsync(AgentTrigger.Start);
    }
    
    // 反序列化恢复
    public void LoadContext(WorkflowContext savedContext)
    {
        _context = savedContext;
        // Stateless 无需显式 SetState，构造函数里会读取 Context 里的 Last State
    }
}