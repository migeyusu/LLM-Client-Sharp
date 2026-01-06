namespace LLMClient.Workflow.Scheme;

//Planner 输出：可执行化
public sealed record PlanStep(
    string StepId,
    string Goal,
    IReadOnlyList<string> FilesToCreateOrModify,
    string Constraints,
    string DefinitionOfDone
);

// Planner 输出：整体实施方案
public sealed record ImplementationPlan(
    string TaskId,
    string PlanSummary,
    IReadOnlyList<PlanStep> Steps,
    IReadOnlyList<string> ExpectedFiles,
    string ValidationPlan
);