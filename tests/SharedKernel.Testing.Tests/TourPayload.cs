namespace PandaBear.Testing.Tests;

/// <summary>A response body for the success assertion tests.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="StartCity">The start city, whose web name is <c>startCity</c>.</param>
internal sealed record TourPayload(int Id, string StartCity);
