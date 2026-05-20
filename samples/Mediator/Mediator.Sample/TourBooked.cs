using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Announces that the sample booking command completed.
/// </summary>
public sealed record TourBooked(string BookingCode, string TravellerName) : INotification;
