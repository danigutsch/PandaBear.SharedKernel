using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>Unvalidated text of an offering in one culture. Lists are one line per item; <see langword="null"/> is none.</summary>
public sealed record OfferingTranslationDefinition
{
    /// <summary>Gets the culture name, one of the rules' cultures.</summary>
    public string? Culture { get; init; }

    /// <summary>Gets whether the translation is published.</summary>
    public PublicationStatus Status { get; init; }

    /// <summary>Gets the slug of the offering's address; blank derives it from the title.</summary>
    public string? Slug { get; init; }

    /// <summary>Gets the title; required.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the optional tagline.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Gets the optional short line above the title, such as a tour's countries.</summary>
    public string? Eyebrow { get; init; }

    /// <summary>Gets the summary, one card paragraph; required to publish.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the description; paragraphs are separated by blank lines; required to publish.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the optional short date note, allowed only when the offering has no dates.</summary>
    public string? DateNote { get; init; }

    /// <summary>Gets the optional search-engine title.</summary>
    public string? SeoTitle { get; init; }

    /// <summary>Gets the optional search-engine description.</summary>
    public string? SeoDescription { get; init; }

    /// <summary>Gets the highlights.</summary>
    public IReadOnlyList<string?>? Highlights { get; init; }

    /// <summary>Gets what is included.</summary>
    public IReadOnlyList<string?>? Included { get; init; }

    /// <summary>Gets what is not included.</summary>
    public IReadOnlyList<string?>? NotIncluded { get; init; }

    /// <summary>Gets the practical information.</summary>
    public IReadOnlyList<string?>? PracticalInfo { get; init; }

    /// <summary>Gets one label per price whose kind needs one, in price order; required to publish.</summary>
    public IReadOnlyList<string?>? PriceLabels { get; init; }

    /// <summary>Gets the text of each step, in step order; one per step is required to publish.</summary>
    public IReadOnlyList<ProgramStepTextDefinition>? StepTexts { get; init; }
}
