namespace SharedKernel.Offerings;

/// <summary>
/// A count's composite format in the two plural forms Portuguese and English need: <paramref name="One"/> for exactly one, and
/// <paramref name="Other"/> for every other count, such as <c>{0} dia</c> and <c>{0} dias</c>. <c>{0}</c> is the count.
/// </summary>
/// <param name="One">The format for one.</param>
/// <param name="Other">The format for every other count.</param>
public sealed record PluralFormat(string One, string Other)
{
    /// <summary>Gets the format for one.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string One { get; } = FormatText.Required(One, nameof(One));

    /// <summary>Gets the format for every other count.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Other { get; } = FormatText.Required(Other, nameof(Other));

    /// <summary>Formats a count.</summary>
    /// <param name="count">The count.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public string Format(int count, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return string.Format(provider, count == 1 ? One : Other, count);
    }
}
