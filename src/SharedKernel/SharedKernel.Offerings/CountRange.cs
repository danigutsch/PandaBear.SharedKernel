namespace SharedKernel.Offerings;

/// <summary>An inclusive range of item counts, such as three to ten highlights.</summary>
public sealed record CountRange
{
    /// <summary>Initializes the range.</summary>
    /// <param name="min">The fewest items, at least 1.</param>
    /// <param name="max">The most items, at least <paramref name="min"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is below 1, or <paramref name="max"/> is below it.</exception>
    public CountRange(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(min);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);

        Min = min;
        Max = max;
    }

    /// <summary>Gets the fewest items.</summary>
    public int Min { get; }

    /// <summary>Gets the most items.</summary>
    public int Max { get; }
}
