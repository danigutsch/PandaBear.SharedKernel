namespace PandaBear.Offerings;

/// <summary>The application's sentence for a group condition (see <see cref="OfferingText.GroupCondition"/>).</summary>
/// <param name="Sentence">
/// The sentence: <c>{0}</c> the minimum participants in <paramref name="Minimum"/>'s form, <c>{1}</c> the surcharge as money for people,
/// <c>{2}</c> the due days in <paramref name="DueDays"/>' form, such as <c>Valores para no mínimo {0}; com menos, {1} por pessoa a menos,
/// confirmado {2} antes.</c>
/// </param>
/// <param name="Minimum">The form of the minimum participants, such as <c>{0} participante</c> and <c>{0} participantes</c>.</param>
/// <param name="DueDays">The form of the due days, such as <c>{0} dia</c> and <c>{0} dias</c>.</param>
public sealed record GroupConditionFormats(string Sentence, PluralFormat Minimum, PluralFormat DueDays)
{
    /// <summary>Gets the sentence.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Sentence { get; } = FormatText.Required(Sentence, nameof(Sentence));

    /// <summary>Gets the form of the minimum participants.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat Minimum { get; } = FormatText.Present(Minimum, nameof(Minimum));

    /// <summary>Gets the form of the due days.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat DueDays { get; } = FormatText.Present(DueDays, nameof(DueDays));
}
