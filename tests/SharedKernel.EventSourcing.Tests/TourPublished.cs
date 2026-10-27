namespace PandaBear.EventSourcing.Tests;

/// <summary>A test domain event recording that a tour was published.</summary>
/// <param name="TourId">The identifier of the published tour.</param>
internal sealed record TourPublished(string TourId);
