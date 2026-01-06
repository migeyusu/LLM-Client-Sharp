namespace LLMClient.Workflow.Scheme;

public sealed record ChangedFile(
    string Path,
    string Content,      // 完整文件内容（Air 模式建议全量）
    string Rationale
);

public sealed record ChangeSet(
    string TaskId,
    IReadOnlyList<ChangedFile> Files,
    string Notes
);
