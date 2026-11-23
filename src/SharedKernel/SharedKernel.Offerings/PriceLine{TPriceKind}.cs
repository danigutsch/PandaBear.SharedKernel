using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>One price of an offering: its kind, from the application's price kinds, and its amount.</summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <param name="Kind">The kind.</param>
/// <param name="Amount">The amount, in the offering's currency.</param>
public sealed record PriceLine<TPriceKind>(TPriceKind Kind, Money Amount)
    where TPriceKind : struct, Enum;
