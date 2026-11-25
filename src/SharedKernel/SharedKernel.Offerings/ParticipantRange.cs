namespace SharedKernel.Offerings;

/// <summary>The smallest and largest group an offering takes; either may be absent. It is also the entered value.</summary>
/// <param name="Min">The smallest group.</param>
/// <param name="Max">The largest group, not smaller than <paramref name="Min"/>.</param>
public sealed record ParticipantRange(int? Min, int? Max);
