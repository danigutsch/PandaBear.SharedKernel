namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>Union-find over item indexes that refuses unions joining a forbidden pair.</summary>
/// <param name="count">Number of items.</param>
/// <param name="forbidden">Index pairs that must never share a set.</param>
internal sealed class ConstrainedUnionFind(int count, IReadOnlyList<(int First, int Second)> forbidden)
{
    private readonly int[] parents = [.. Enumerable.Range(0, count)];

    /// <summary>Finds the representative of an index.</summary>
    /// <param name="index">Item index.</param>
    /// <returns>The representative index.</returns>
    public int Find(int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }

        return index;
    }

    /// <summary>Joins the sets of two indexes unless that would join a forbidden pair.</summary>
    /// <param name="first">First index.</param>
    /// <param name="second">Second index.</param>
    /// <returns><see langword="true"/> when both indexes now share a set.</returns>
    public bool Union(int first, int second)
    {
        int firstRoot = Find(first);
        int secondRoot = Find(second);
        if (firstRoot == secondRoot)
        {
            return true;
        }

        foreach (var (a, b) in forbidden)
        {
            int rootA = Find(a);
            int rootB = Find(b);
            if ((rootA == firstRoot && rootB == secondRoot) || (rootA == secondRoot && rootB == firstRoot))
            {
                return false;
            }
        }

        // The smaller root wins, so representatives do not depend on union order.
        if (firstRoot < secondRoot)
        {
            parents[secondRoot] = firstRoot;
        }
        else
        {
            parents[firstRoot] = secondRoot;
        }

        return true;
    }
}
