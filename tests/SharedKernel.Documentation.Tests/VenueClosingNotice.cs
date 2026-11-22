namespace PandaBear.Documentation.Tests;

/// <summary>A fictional event announcing the venue's closing time.</summary>
/// <param name="Room">The room name on the front desk schedule.</param>
/// <param name="DoorsCloseAt">The published door closing time.</param>
/// <param name="NextNotice">The next scheduled closing notice, if any.</param>
internal sealed record VenueClosingNotice(string Room, DateTimeOffset DoorsCloseAt, VenueClosingNotice? NextNotice);
