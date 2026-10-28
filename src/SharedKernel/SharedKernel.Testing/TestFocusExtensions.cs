namespace PandaBear.Testing;

/// <summary>
/// Maps <see cref="TestFocus" /> to its <c>Focus</c> trait value.
/// </summary>
public static class TestFocusExtensions
{
    /// <summary>
    /// Gets the <c>Focus</c> trait value for <paramref name="focus" />, the kebab-case member name.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="focus" /> is not a defined value.</exception>
    public static string ToTraitValue(this TestFocus focus) => TraitValue.Of(focus, nameof(focus));
}
