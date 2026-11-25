namespace SharedKernel.Offerings;

/// <summary>The language-neutral part of one step of an offering's programme, such as a day of a tour: its image and its metrics.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
public sealed class ProgramStep<TImageId, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TMetric : struct, Enum
{
    /// <summary>Initializes a step.</summary>
    /// <param name="imageId">The image.</param>
    /// <param name="metrics">The metrics, in rule order.</param>
    internal ProgramStep(TImageId? imageId, IReadOnlyList<Metric<TMetric>> metrics)
    {
        ImageId = imageId;
        Metrics = metrics;
    }

    /// <summary>Gets the optional image of the step.</summary>
    public TImageId? ImageId { get; }

    /// <summary>Gets the step's metrics, in the rules' order.</summary>
    public IReadOnlyList<Metric<TMetric>> Metrics { get; }

    /// <summary>Determines whether another step has the same data.</summary>
    /// <param name="other">The other step.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    internal bool SameAs(ProgramStep<TImageId, TMetric> other) =>
        Nullable.Equals(ImageId, other.ImageId) && Metrics.SequenceEqual(other.Metrics);
}
