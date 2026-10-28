namespace PandaBear.Testing;

/// <summary>
/// Maps <see cref="TestHost" /> to its <c>Host</c> trait value.
/// </summary>
public static class TestHostExtensions
{
    /// <summary>
    /// Gets the <c>Host</c> trait value for <paramref name="host" />, the kebab-case member name.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="host" /> is not a defined value.</exception>
    public static string ToTraitValue(this TestHost host) => TraitValue.Of(host, nameof(host));
}
