namespace PandaBear.Offerings;

/// <summary>One group of the offering listing: the offerings of a year, or those on request.</summary>
/// <typeparam name="TItem">The listed item, such as an <see cref="OfferingView{TImageId, TPriceKind, TMetric}"/>.</typeparam>
/// <param name="Kind">The kind of group.</param>
/// <param name="Year">The year of a <see cref="OfferingGroupKind.Year"/> group; <see langword="null"/> for the group on request.</param>
/// <param name="Items">The items in display order.</param>
public sealed record OfferingGroup<TItem>(OfferingGroupKind Kind, int? Year, IReadOnlyList<TItem> Items);
