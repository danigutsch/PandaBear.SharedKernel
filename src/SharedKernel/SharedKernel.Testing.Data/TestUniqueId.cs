using System.Globalization;

namespace SharedKernel.Testing.Data;

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
    public static string Create(string prefix = "test")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}-{timestamp}-{Guid.NewGuid():N}");
    }
}
