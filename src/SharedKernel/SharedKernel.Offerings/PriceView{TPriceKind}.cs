using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>One price of a public view.</summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <param name="Kind">The kind; the application names a kind without its own label with its site-wide label.</param>
/// <param name="Amount">The amount.</param>
/// <param name="Label">The price's own label in the view's culture, for a kind that needs one; otherwise <see langword="null"/>.</param>
/// <param name="IsBase">Whether the kind is a base kind, one that can be the from price.</param>
public sealed record PriceView<TPriceKind>(TPriceKind Kind, Money Amount, string? Label, bool IsBase)
    where TPriceKind : struct, Enum;
