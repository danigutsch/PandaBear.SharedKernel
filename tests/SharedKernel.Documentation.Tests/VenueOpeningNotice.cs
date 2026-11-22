using System.Text.Json.Serialization;

namespace PandaBear.Documentation.Tests;

/// <summary>A fictional event published by a community venue.</summary>
/// <param name="Room">The room name on the front desk schedule.</param>
/// <param name="DoorsOpenAt">The published door opening time.</param>
/// <param name="Poster">An optional relative poster address.</param>
internal sealed record VenueOpeningNotice(
    [property: JsonPropertyName("venueRoom")] string Room,
    DateTimeOffset DoorsOpenAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Uri? Poster);
