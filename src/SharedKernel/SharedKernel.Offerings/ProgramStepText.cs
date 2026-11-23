namespace PandaBear.Offerings;

/// <summary>The text of one step in one culture: a title without its number, paragraphs, and a short note such as "+ barco" or "40 ou 53 km".</summary>
public sealed class ProgramStepText
{
    /// <summary>Initializes the text.</summary>
    /// <param name="title">The title.</param>
    /// <param name="paragraphs">The paragraphs.</param>
    /// <param name="note">The note.</param>
    internal ProgramStepText(string title, IReadOnlyList<string> paragraphs, string? note)
    {
        Title = title;
        Paragraphs = paragraphs;
        Note = note;
    }

    /// <summary>Gets the title; the step's number comes from its position.</summary>
    public string Title { get; }

    /// <summary>Gets the paragraphs, possibly none.</summary>
    public IReadOnlyList<string> Paragraphs { get; }

    /// <summary>Gets the optional free-text note.</summary>
    public string? Note { get; }

    /// <summary>Determines whether another text has the same data.</summary>
    /// <param name="other">The other text.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    internal bool SameAs(ProgramStepText other) =>
        string.Equals(Title, other.Title, StringComparison.Ordinal)
        && string.Equals(Note, other.Note, StringComparison.Ordinal)
        && Paragraphs.SequenceEqual(other.Paragraphs, StringComparer.Ordinal);
}
