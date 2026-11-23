namespace PandaBear.Offerings;

/// <summary>The application's sentences for a group size (see <see cref="OfferingText.Participants"/>).</summary>
/// <param name="Range">The format of a smallest and a largest group, <c>{0}</c> and <c>{1}</c>, such as <c>{0} a {1} participantes</c>.</param>
/// <param name="Minimum">The format of a smallest group alone.</param>
/// <param name="Maximum">The format of a largest group alone.</param>
/// <param name="Exact">The format of one fixed size, when the smallest and largest group are equal.</param>
public sealed record ParticipantFormats(string Range, PluralFormat Minimum, PluralFormat Maximum, PluralFormat Exact)
{
    /// <summary>Gets the format of a range.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Range { get; } = FormatText.Required(Range, nameof(Range));

    /// <summary>Gets the format of a smallest group alone.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat Minimum { get; } = FormatText.Present(Minimum, nameof(Minimum));

    /// <summary>Gets the format of a largest group alone.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat Maximum { get; } = FormatText.Present(Maximum, nameof(Maximum));

    /// <summary>Gets the format of one fixed size.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat Exact { get; } = FormatText.Present(Exact, nameof(Exact));
}
