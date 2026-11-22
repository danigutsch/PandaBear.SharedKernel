using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>The validated text of an offering in one culture. Lists are read-only and possibly empty.</summary>
public sealed class OfferingTranslation
{
    /// <summary>Initializes a translation from validated or restored parts.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="status">The status.</param>
    /// <param name="slug">The slug.</param>
    /// <param name="title">The title.</param>
    internal OfferingTranslation(ContentCulture culture, PublicationStatus status, Slug slug, string title)
    {
        Culture = culture;
        Status = status;
        Slug = slug;
        Title = title;
    }

    /// <summary>Gets the culture.</summary>
    public ContentCulture Culture { get; }

    /// <summary>Gets whether the translation is published.</summary>
    public PublicationStatus Status { get; }

    /// <summary>Gets the slug of the offering's address in this culture.</summary>
    public Slug Slug { get; }

    /// <summary>Gets the title, used by the hero, the card, the menu and, without an SEO title, the search engines.</summary>
    public string Title { get; }

    /// <summary>Gets the optional tagline.</summary>
    public string? Subtitle { get; internal init; }

    /// <summary>Gets the optional short line above the title, such as a tour's countries in "A, B e C" form.</summary>
    public string? Eyebrow { get; internal init; }

    /// <summary>Gets the summary, one card paragraph.</summary>
    public string? Summary { get; internal init; }

    /// <summary>Gets the description's paragraphs.</summary>
    public IReadOnlyList<string> Description { get; internal init; } = [];

    /// <summary>Gets the optional date note of an undated offering.</summary>
    public string? DateNote { get; internal init; }

    /// <summary>Gets the optional search-engine title.</summary>
    public string? SeoTitle { get; internal init; }

    /// <summary>Gets the optional search-engine description.</summary>
    public string? SeoDescription { get; internal init; }

    /// <summary>Gets the highlights.</summary>
    public IReadOnlyList<string> Highlights { get; internal init; } = [];

    /// <summary>Gets what is included.</summary>
    public IReadOnlyList<string> Included { get; internal init; } = [];

    /// <summary>Gets what is not included.</summary>
    public IReadOnlyList<string> NotIncluded { get; internal init; } = [];

    /// <summary>Gets the practical information.</summary>
    public IReadOnlyList<string> PracticalInfo { get; internal init; } = [];

    /// <summary>
    /// Gets one label per price whose kind needs one, in the offering's price order; <see langword="null"/> marks a missing label, and
    /// trailing missing ones are left out, so a draft may have fewer.
    /// </summary>
    public IReadOnlyList<string?> PriceLabels { get; internal init; } = [];

    /// <summary>Gets the text of each step, in step order; fewer than the steps while a draft is incomplete.</summary>
    public IReadOnlyList<ProgramStepText> StepTexts { get; internal init; } = [];

    /// <summary>Determines whether another translation has the same data.</summary>
    /// <param name="other">The other translation.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    internal bool SameAs(OfferingTranslation other) =>
        Culture == other.Culture
        && Status == other.Status
        && Slug == other.Slug
        && Same(Title, other.Title)
        && Same(Subtitle, other.Subtitle)
        && Same(Eyebrow, other.Eyebrow)
        && Same(Summary, other.Summary)
        && Same(DateNote, other.DateNote)
        && Same(SeoTitle, other.SeoTitle)
        && Same(SeoDescription, other.SeoDescription)
        && Description.SequenceEqual(other.Description, StringComparer.Ordinal)
        && Highlights.SequenceEqual(other.Highlights, StringComparer.Ordinal)
        && Included.SequenceEqual(other.Included, StringComparer.Ordinal)
        && NotIncluded.SequenceEqual(other.NotIncluded, StringComparer.Ordinal)
        && PracticalInfo.SequenceEqual(other.PracticalInfo, StringComparer.Ordinal)
        && PriceLabels.SequenceEqual(other.PriceLabels, StringComparer.Ordinal)
        && StepTexts.Count == other.StepTexts.Count
        && StepTexts.Zip(other.StepTexts).All(pair => pair.First.SameAs(pair.Second));

    private static bool Same(string? first, string? second) => string.Equals(first, second, StringComparison.Ordinal);
}
