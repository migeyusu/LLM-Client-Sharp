namespace LLMClient.Workflow.Scheme;

/// <summary>
/// （TaskGenerator 输出）
/// </summary>
public sealed record TaskContract(
    string TaskId,
    string Title,
    string Goal
);