namespace LLMClient.Abstraction;

public class AIFunctionGroupComparer : IEqualityComparer<IAIFunctionGroup>
{
    public static AIFunctionGroupComparer Instance => new AIFunctionGroupComparer();

    public bool Equals(IAIFunctionGroup? x, IAIFunctionGroup? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        return x.GetUniqueId() == y.GetUniqueId();
    }

    public int GetHashCode(IAIFunctionGroup obj)
    {
        // return obj.GetUniqueId().GetHashCode();
        return HashCode.Combine(obj.GetUniqueId());
    }
}