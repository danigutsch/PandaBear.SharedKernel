namespace PandaBear.Offerings;

/// <summary>Groups offerings for a listing page.</summary>
public static class OfferingListing
{
    /// <summary>
    /// Groups public views: one group per year, ascending, for the scheduled, sold-out and to-be-confirmed offerings, then one group of
    /// the offerings on request. Within a group the order is the sort order, then the first day, undated offerings last.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="views">The views.</param>
    /// <returns>The groups; a group without items is left out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="views"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<OfferingGroup<OfferingView<TImageId, TPriceKind, TMetric>>> Group<TImageId, TPriceKind, TMetric>(
        IEnumerable<OfferingView<TImageId, TPriceKind, TMetric>> views)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum =>
        Group(views, view => view.SortOrder, view => view.Schedule);

    /// <summary>
    /// Groups the application's own items, such as a card model that wraps a view, as
    /// <see cref="Group{TImageId, TPriceKind, TMetric}(IEnumerable{OfferingView{TImageId, TPriceKind, TMetric}})"/> does.
    /// </summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="sortOrder">Reads an item's sort order.</param>
    /// <param name="schedule">Reads an item's schedule.</param>
    /// <returns>The groups; a group without items is left out.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A schedule's availability is <see cref="OfferingAvailability.Unspecified"/> or not defined.</exception>
    public static IReadOnlyList<OfferingGroup<TItem>> Group<TItem>(IEnumerable<TItem> items, Func<TItem, int> sortOrder, Func<TItem, OfferingSchedule> schedule)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sortOrder);
        ArgumentNullException.ThrowIfNull(schedule);

        var ordered = items
            .Select(item => (Item: item, Schedule: Checked(schedule(item))))
            .OrderBy(entry => sortOrder(entry.Item))
            .ThenBy(entry => entry.Schedule.Dates is null)
            .ThenBy(entry => entry.Schedule.Dates?.StartDate)
            .ToList();
        var years = ordered
            .Where(entry => entry.Schedule.Availability != OfferingAvailability.OnRequest)
            .GroupBy(entry => entry.Schedule.Year)
            .OrderBy(group => group.Key is null)
            .ThenBy(group => group.Key)
            .Select(group => new OfferingGroup<TItem>(OfferingGroupKind.Year, group.Key, [.. group.Select(entry => entry.Item)]));
        var onRequest = ordered.Where(entry => entry.Schedule.Availability == OfferingAvailability.OnRequest).Select(entry => entry.Item).ToList();
        return onRequest.Count == 0
            ? [.. years]
            : [.. years, new OfferingGroup<TItem>(OfferingGroupKind.OnRequest, null, onRequest.AsReadOnly())];
    }

    private static OfferingSchedule Checked(OfferingSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule, nameof(schedule));
        OfferingSchedules.Check(schedule, nameof(schedule));
        return schedule;
    }
}
