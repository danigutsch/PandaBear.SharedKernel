namespace PandaBear.Offerings;

/// <summary>How an offering is paid; each part is optional. It is also the entered value.</summary>
/// <param name="DepositPercent">The deposit on booking, as a percentage of the price.</param>
/// <param name="Instalments">The most card instalments.</param>
/// <param name="BalanceDueDays">How many days before the start the balance is due.</param>
public sealed record PaymentTerms(int? DepositPercent, int? Instalments, int? BalanceDueDays);
