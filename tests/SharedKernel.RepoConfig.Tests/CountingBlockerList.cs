using System.Collections;

namespace PandaBear.RepoConfig.Tests;

/// <summary>A read-only list that calls a callback each time an element is read by index or enumeration.</summary>
/// <param name="values">The elements to expose.</param>
/// <param name="onRead">Runs once per element read.</param>
internal sealed class CountingBlockerList(IReadOnlyList<string> values, Action onRead) : IReadOnlyList<string>
{
    /// <summary>The number of elements, which does not count as a read.</summary>
    public int Count => values.Count;

    public string this[int index]
    {
        get
        {
            onRead();
            return values[index];
        }
    }

    /// <summary>Enumerates the elements and reports each one to the read callback.</summary>
    public IEnumerator<string> GetEnumerator()
    {
        foreach (var value in values)
        {
            onRead();
            yield return value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
