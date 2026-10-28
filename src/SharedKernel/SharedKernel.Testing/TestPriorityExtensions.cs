namespace PandaBear.Testing;

/// <summary>
/// Maps <see cref="TestPriority" /> to its <c>Priority</c> trait value.
/// </summary>
public static class TestPriorityExtensions
{
    /// <summary>
    /// Gets the <c>Priority</c> trait value for <paramref name="priority" />, the kebab-case member name.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="priority" /> is not a defined value.</exception>
    public static string ToTraitValue(this TestPriority priority) => TraitValue.Of(priority, nameof(priority));
}
