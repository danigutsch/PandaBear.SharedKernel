using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Produces a simple query response for the sample tour summary flow.
/// </summary>
public sealed class LookupTourSummaryHandler : IRequestHandler<LookupTourSummary, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(LookupTourSummary request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(
            $"Tour {request.TourCode}: Patagonia Explorer with generated mediator dispatch.");
    }
}
