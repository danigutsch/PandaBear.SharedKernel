namespace SharedKernel.Offerings;

/// <summary>The application's sentences for payment terms (see <see cref="OfferingText.PaymentTerms"/>).</summary>
/// <param name="Deposit">The format of the deposit percentage, <c>{0}</c>, such as <c>Sinal de {0}%</c>.</param>
/// <param name="Instalments">The format of the most instalments.</param>
/// <param name="BalanceDue">The format of the days before the start the balance is due.</param>
public sealed record PaymentTermsFormats(string Deposit, PluralFormat Instalments, PluralFormat BalanceDue)
{
    /// <summary>Gets the format of the deposit.</summary>
    /// <exception cref="ArgumentException">The format is <see langword="null"/>, empty or blank.</exception>
    public string Deposit { get; } = FormatText.Required(Deposit, nameof(Deposit));

    /// <summary>Gets the format of the instalments.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat Instalments { get; } = FormatText.Present(Instalments, nameof(Instalments));

    /// <summary>Gets the format of the balance due.</summary>
    /// <exception cref="ArgumentNullException">The format is <see langword="null"/>.</exception>
    public PluralFormat BalanceDue { get; } = FormatText.Present(BalanceDue, nameof(BalanceDue));
}
