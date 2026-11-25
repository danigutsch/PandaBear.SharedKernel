using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Produces a booking code for the sample command flow.
/// </summary>
public sealed class CreateBookingHandler : IRequestHandler<CreateBooking, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(CreateBooking request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var initials = string.Concat(
            request.TravellerName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => char.ToUpperInvariant(part[0])));

        return ValueTask.FromResult($"{request.TourCode}-{initials}-001");
    }
}
