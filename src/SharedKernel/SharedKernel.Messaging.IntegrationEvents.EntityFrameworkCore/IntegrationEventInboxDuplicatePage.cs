namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>A bounded page of completed duplicate observations for one consumer.</summary>
/// <param name="Rows">The metadata for the current page.</param>
/// <param name="Next">The next position, or <see langword="null"/> when there are no more rows.</param>
public readonly record struct IntegrationEventInboxDuplicatePage(
    IReadOnlyList<IntegrationEventInboxDuplicateRow> Rows,
    IntegrationEventInboxDuplicateCursor? Next);
