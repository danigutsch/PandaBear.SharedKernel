using System.Collections;

namespace SharedKernel.Testing.SourceGenerator;

/// <summary>An immutable array compared by its items, so incremental generator models that hold it cache correctly.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="items">The items; the array must not change afterwards.</param>
internal readonly struct EquatableArray<T>(T[] items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[]? _items = items;

    /// <summary>Gets the number of items.</summary>
    public int Count => _items?.Length ?? 0;

    /// <summary>Gets the item at an index.</summary>
    /// <param name="index">The index.</param>
    public T this[int index] => _items![index];

    /// <inheritdoc />
    public bool Equals(EquatableArray<T> other)
    {
        if (Count != other.Count)
        {
            return false;
        }

        for (var index = 0; index < Count; index++)
        {
            if (!_items![index].Equals(other._items![index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
        {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
