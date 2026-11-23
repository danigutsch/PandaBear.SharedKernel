namespace PandaBear.Offerings;

/// <summary>Unvalidated language-neutral part of a step.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
/// <param name="ImageId">The optional image, which must exist.</param>
/// <param name="Metrics">The step's metrics, each of a kind allowed on steps; <see langword="null"/> is none.</param>
public sealed record ProgramStepDefinition<TImageId, TMetric>(TImageId? ImageId, IReadOnlyList<Metric<TMetric>>? Metrics = null)
    where TImageId : struct, IEquatable<TImageId>
    where TMetric : struct, Enum;
