using SharedKernel.Mediator;

namespace Mediator.Sample;

/// <summary>
/// Requests a readable summary for a tour code.
/// </summary>
public sealed record LookupTourSummary(string TourCode) : IQuery<string>;
