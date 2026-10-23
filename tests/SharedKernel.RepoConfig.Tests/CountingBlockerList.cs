using System.Collections;

namespace PandaBear.RepoConfig.Tests;

internal sealed class CountingBlockerList(IReadOnlyList<string> values, Action onRead) : IReadOnlyList<string>
{
    public int Count => values.Count;

    public string this[int index]
    {
        get
        {
            onRead();
            return values[index];
        }
    }

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
