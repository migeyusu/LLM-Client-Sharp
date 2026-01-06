namespace LLMClient.Workflow.Scheme;

//DesignCandidate / DesignCandidates（多路并行产出）
public sealed record DesignCandidate(
    string CandidateId,
    string ModelInfo, // "o3", "deepseek-r1", "gpt-5"...
    string Summary, // 设计概要
    string Design, // 完整设计文本
    string UmlText, // 文本化类图/关系
    string DataFlow, // 关键流程（文本化时序）
    string Risks // 潜在风险
);

public sealed record DesignCandidates(
    string TaskId,
    int Iteration, // 0 or 1
    IReadOnlyList<DesignCandidate> Candidates
);