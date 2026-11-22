namespace PandaBear.Offerings;

/// <summary>The application's rule for one price kind. The order of the rules is the display order of the prices.</summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <param name="Kind">The kind.</param>
/// <param name="Repeatable">Whether an offering may have several prices of this kind, kept in their entered order.</param>
/// <param name="NeedsLabel">Whether each price of this kind needs its own label per translation (such as an "other" kind), instead of the application's site-wide label.</param>
/// <param name="IsBase">Whether a price of this kind can be the "from" price.</param>
public sealed record PriceKindRule<TPriceKind>(TPriceKind Kind, bool Repeatable = false, bool NeedsLabel = false, bool IsBase = false)
    where TPriceKind : struct, Enum;
