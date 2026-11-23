namespace PandaBear.PageSections;

/// <summary>A question of a section's frequently asked questions, with its answer's paragraphs.</summary>
public sealed class PageQuestion : IEquatable<PageQuestion>
{
    /// <summary>Initializes a question.</summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer's paragraphs.</param>
    internal PageQuestion(string question, IReadOnlyList<string> answer)
    {
        Question = question;
        Answer = answer;
    }

    /// <summary>Gets the question, one line.</summary>
    public string Question { get; }

    /// <summary>Gets the paragraphs of the answer, at least one.</summary>
    public IReadOnlyList<string> Answer { get; }

    /// <inheritdoc />
    public bool Equals(PageQuestion? other) =>
        other is not null && string.Equals(Question, other.Question, StringComparison.Ordinal) && Answer.SequenceEqual(other.Answer, StringComparer.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PageQuestion);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(Question), Answer.Count);
}
