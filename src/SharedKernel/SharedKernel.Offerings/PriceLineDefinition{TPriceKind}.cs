namespace PandaBear.Offerings;

/// <summary>Unvalidated price of an offering.</summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <param name="Kind">The kind, one of the rules' price kinds.</param>
/// <param name="Amount">The amount as typed, with the decimal places it was written with (see <c>Money.CreateFromInput</c>).</param>
public sealed record PriceLineDefinition<TPriceKind>(TPriceKind Kind, decimal Amount)
    where TPriceKind : struct, Enum;
