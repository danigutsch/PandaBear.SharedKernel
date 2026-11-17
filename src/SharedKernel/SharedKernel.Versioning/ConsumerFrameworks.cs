namespace PandaBear.Versioning;

/// <summary>The target frameworks that packed consumers are built for.</summary>
public static class ConsumerFrameworks
{
    /// <summary>
    /// Determines whether the value is a simple target framework moniker: <c>net</c>, a major version, a dot, and a minor
    /// version, in lowercase, such as <c>net10.0</c>.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is such a moniker; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(string? value)
    {
        if (value is null || !value.StartsWith("net", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[3..].Split('.');
        return parts.Length == 2 && parts.All(static part => part.Length is > 0 and <= 3 && part.All(char.IsAsciiDigit));
    }
}
