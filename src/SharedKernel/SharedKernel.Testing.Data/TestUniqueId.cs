using System.Globalization;

namespace PandaBear.Testing.Data;

/// <summary>
/// Creates deterministic-format unique identifiers for test-owned data.
/// </summary>
public static class TestUniqueId
{
    /// <summary>
    /// Creates a compact unique suffix for a test-owned entity.
    /// </summary>
    /// <param name="prefix">The optional prefix.</param>
    /// <returns>A unique value safe for names, identifiers, and email local parts.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is <see langword="null"/>, empty, or consists only of white-space characters.</exception>
    public static string Create(string prefix = "test")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var timestamp = TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}-{timestamp}-{Guid.NewGuid():N}");
    }
}
