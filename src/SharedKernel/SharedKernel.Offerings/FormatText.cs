namespace PandaBear.Offerings;

/// <summary>Checks the composite formats an application gives.</summary>
internal static class FormatText
{
    /// <summary>Returns a format that is present and not blank.</summary>
    /// <param name="format">The format.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The format.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="format"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="format"/> is empty or blank.</exception>
    public static string Required(string format, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format, name);
        return format;
    }

    /// <summary>Returns a value that is present.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static T Present<T>(T value, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }
}
