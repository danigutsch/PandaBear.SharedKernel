using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Represents the sample command used to create a booking identifier.
/// </summary>
public sealed record CreateBooking(string TourCode, string TravellerName) : ICommand<string>;
