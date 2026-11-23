namespace PandaBear.Offerings;

/// <summary>One step of a public view: its number from its position, its text in the view's culture, and its image and metrics.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
public sealed class ProgramStepView<TImageId, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TMetric : struct, Enum
{
    /// <summary>Initializes the view of a step.</summary>
    /// <param name="number">The number, from 1.</param>
    /// <param name="step">The language-neutral step.</param>
    /// <param name="text">The text.</param>
    internal ProgramStepView(int number, ProgramStep<TImageId, TMetric> step, ProgramStepText text)
    {
        Number = number;
        Title = text.Title;
        Paragraphs = text.Paragraphs;
        Note = text.Note;
        ImageId = step.ImageId;
        Metrics = step.Metrics;
    }

    /// <summary>Gets the number, from 1, as "Dia 1" shows it.</summary>
    public int Number { get; }

    /// <summary>Gets the title.</summary>
    public string Title { get; }

    /// <summary>Gets the paragraphs.</summary>
    public IReadOnlyList<string> Paragraphs { get; }

    /// <summary>Gets the optional note.</summary>
    public string? Note { get; }

    /// <summary>Gets the optional image.</summary>
    public TImageId? ImageId { get; }

    /// <summary>Gets the metrics, in the rules' order.</summary>
    public IReadOnlyList<Metric<TMetric>> Metrics { get; }
}
