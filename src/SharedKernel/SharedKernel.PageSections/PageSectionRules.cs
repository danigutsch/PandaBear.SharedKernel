namespace PandaBear.PageSections;

/// <summary>The limits of page sections. The defaults are ViajantesTurismo V2's.</summary>
public sealed class PageSectionRules
{
    private readonly int _maxSections = 60;
    private readonly int _maxQuestions = 30;
    private readonly int _maxQuotes = 60;
    private readonly int _maxImages = 60;
    private readonly int _maxParagraphs = 50;
    private readonly int _maxParagraphLength = 4000;
    private readonly int _maxTitleLength = 200;
    private readonly int _maxLineLength = 300;
    private readonly int _maxQuoteLength = 1000;

    /// <summary>Gets the most sections of one translation. The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxSections { get => _maxSections; init => _maxSections = Positive(value); }

    /// <summary>Gets the most questions of a section. The default is 30.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxQuestions { get => _maxQuestions; init => _maxQuestions = Positive(value); }

    /// <summary>Gets the most quotes of a section. The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxQuotes { get => _maxQuotes; init => _maxQuotes = Positive(value); }

    /// <summary>Gets the most images of a section. The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxImages { get => _maxImages; init => _maxImages = Positive(value); }

    /// <summary>Gets the most paragraphs of a section's text or an answer. The default is 50.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxParagraphs { get => _maxParagraphs; init => _maxParagraphs = Positive(value); }

    /// <summary>Gets the longest paragraph. The default is 4000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxParagraphLength { get => _maxParagraphLength; init => _maxParagraphLength = Positive(value); }

    /// <summary>Gets the longest heading, person name and role, quote source, video title and button text. The default is 200.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxTitleLength { get => _maxTitleLength; init => _maxTitleLength = Positive(value); }

    /// <summary>Gets the longest eyebrow and question. The default is 300.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxLineLength { get => _maxLineLength; init => _maxLineLength = Positive(value); }

    /// <summary>Gets the longest quote. The default is 1000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxQuoteLength { get => _maxQuoteLength; init => _maxQuoteLength = Positive(value); }

    private static int Positive(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value;
    }
}
