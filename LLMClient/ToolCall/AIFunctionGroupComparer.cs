using LLMClient.Abstraction;

namespace LLMClient.ToolCall;

public class AIFunctionGroupComparer : IEqualityComparer<IAIFunctionGroup>
{
    public static AIFunctionGroupComparer Instance { get; } = new();

    public bool Equals(IAIFunctionGroup? x, IAIFunctionGroup? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;

        x = Unwrap(x);
        y = Unwrap(y);

        if (ReferenceEquals(x, y)) return true;
        if (x.GetType() != y.GetType()) return false;
        return x.GetUniqueId() == y.GetUniqueId();
    }

    public int GetHashCode(IAIFunctionGroup obj)
    {
        obj = Unwrap(obj);
        return HashCode.Combine(obj.GetType(), obj.GetUniqueId());
    }

    private static IAIFunctionGroup Unwrap(IAIFunctionGroup group)
    {
        while (group is CheckableFunctionGroupTree tree)
        {
            group = tree.Data;
        }

        return group;
    }
}