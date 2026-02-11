namespace LLMClient;

public static class ReadOnlyListExtensions
{
    /// <summary>
    /// 在 IReadOnlyList 中查找指定项的索引
    /// </summary>
    public static int IndexOf<T>(this IReadOnlyList<T> list, T item)
    {
        var comparer = EqualityComparer<T>.Default;
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(list[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 支持谓词查找的 IndexOf
    /// </summary>
    public static int FindIndex<T>(this IReadOnlyList<T> list, Predicate<T> match)
    {
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            if (match(list[i]))
            {
                return i;
            }
        }

        return -1;
    }
}