using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Emits a readable confirmation for the sample notification flow.
/// </summary>
public sealed class TourBookedHandler : INotificationHandler<TourBooked>
{
    /// <inheritdoc />
    public ValueTask Handle(TourBooked notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Console.WriteLine($"Notification handled: {notification.BookingCode} for {notification.TravellerName}");
        return ValueTask.CompletedTask;
    }
}
