using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LLMClient.Component.Utility;

public static class ObservableCollectionPatcher
{
    public static void PatchByLcs<T>(
        ObservableCollection<T> target,
        IReadOnlyList<T> desired,
        Func<T, Guid> keySelector)
    {
        // oldKeys / newKeys
        var old = target.ToList();
        var oldKeys = old.Select(keySelector).ToArray();
        var newKeys = desired.Select(keySelector).ToArray();

        // 1) LCS：得到要保留的 key 集合（并保序）
        var lcsKeys = LongestCommonSubsequence(oldKeys, newKeys);

        var lcsSet = new HashSet<Guid>(lcsKeys);

        // 2) 删除：把 old 中不在 LCS 的先移除（从后往前）
        for (int i = target.Count - 1; i >= 0; i--)
        {
            var k = keySelector(target[i]);
            if (!lcsSet.Contains(k))
                target.RemoveAt(i);
        }

        // 此时 target 里只剩 LCS（但位置可能不对），下一步按 desired 重排+插入缺失项
        // 建立 key -> index 的动态映射
        var indexOf = new Dictionary<Guid, int>();
        for (int i = 0; i < target.Count; i++)
            indexOf[keySelector(target[i])] = i;

        // 3) 按 desired 从左到右：存在则 Move 到位；不存在则 Insert
        for (int desiredIndex = 0; desiredIndex < desired.Count; desiredIndex++)
        {
            var desiredItem = desired[desiredIndex];
            var k = keySelector(desiredItem);

            if (desiredIndex < target.Count && keySelector(target[desiredIndex]) == k)
            {
                // already in place
                indexOf[k] = desiredIndex;
                continue;
            }

            if (indexOf.TryGetValue(k, out var currentIndex))
            {
                // Move existing item to desiredIndex
                target.Move(currentIndex, desiredIndex);

                // Move 会改变区间内的索引，更新映射（简单做法：重建映射；对话量不大可接受）
                indexOf.Clear();
                for (int i = 0; i < target.Count; i++)
                    indexOf[keySelector(target[i])] = i;
            }
            else
            {
                // Insert new item
                target.Insert(desiredIndex, desiredItem);

                indexOf.Clear();
                for (int i = 0; i < target.Count; i++)
                    indexOf[keySelector(target[i])] = i;
            }
        }

        // 4) 如果 target 比 desired 长，删尾（一般不会发生，但兜底）
        while (target.Count > desired.Count)
            target.RemoveAt(target.Count - 1);
    }

    // 标准 DP LCS（返回 key 序列）
    private static List<Guid> LongestCommonSubsequence(Guid[] a, Guid[] b)
    {
        int n = a.Length, m = b.Length;
        var dp = new int[n + 1, m + 1];

        for (int i = 1; i <= n; i++)
        for (int j = 1; j <= m; j++)
            dp[i, j] = a[i - 1] == b[j - 1] ? dp[i - 1, j - 1] + 1 : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        // 回溯恢复序列
        var res = new List<Guid>(dp[n, m]);
        int x = n, y = m;
        while (x > 0 && y > 0)
        {
            if (a[x - 1] == b[y - 1])
            {
                res.Add(a[x - 1]);
                x--;
                y--;
            }
            else if (dp[x - 1, y] >= dp[x, y - 1]) x--;
            else y--;
        }

        res.Reverse();
        return res;
    }
}

public class SuspendableObservableCollection<T> : ObservableCollection<T>, ISupportInitialize
{
    private bool _isSuspended;
    private int _suspendCount;

    public SuspendableObservableCollection() : base()
    {
    }

    public SuspendableObservableCollection(IEnumerable<T> enumerable) : base(enumerable)
    {
    }

    public void BeginInit()
    {
        _suspendCount++;
        _isSuspended = true;
    }

    /// <summary>
    /// same as end reset
    /// </summary>
    public void EndInit()
    {
        if (--_suspendCount == 0)
        {
            _isSuspended = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void EndAdd(IReadOnlyList<T> addItems)
    {
        if (--_suspendCount == 0)
        {
            _isSuspended = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)addItems));
        }
    }

    public void AddRange(IList<T> items)
    {
        BeginInit();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        EndInit();
    }

    public void ResetWith(IEnumerable<T> items)
    {
        BeginInit();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        EndInit();
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_isSuspended)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_isSuspended)
            base.OnPropertyChanged(e);
    }
}