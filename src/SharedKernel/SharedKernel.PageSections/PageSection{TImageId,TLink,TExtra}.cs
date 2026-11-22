namespace PandaBear.PageSections;

/// <summary>
/// A validated page section on the band of its tone. Its parts show in a fixed order: the eyebrow and heading, the paragraphs, the
/// questions, the person line, the quotes, the application's extension, the video and the button; the images follow them as a mosaic or
/// stand beside them. Two sections with the same data are equal. Create sections with <see cref="PageSectionList.Create{TImageId, TLink, TExtra}"/>,
/// store one as <see cref="ToDefinition"/>, and rebuild a stored one with <see cref="PageSection.Restore{TImageId, TLink, TExtra}"/>.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLink">The application's link target.</typeparam>
/// <typeparam name="TExtra">The application's extension; <c>default</c> means none.</typeparam>
public sealed class PageSection<TImageId, TLink, TExtra> : IEquatable<PageSection<TImageId, TLink, TExtra>>
    where TImageId : struct, IEquatable<TImageId>
    where TLink : notnull
    where TExtra : struct, IEquatable<TExtra>
{
    /// <summary>Initializes a section from validated or restored parts.</summary>
    internal PageSection()
    {
    }

    /// <summary>Gets the optional short line above the heading.</summary>
    public string? Eyebrow { get; internal init; }

    /// <summary>Gets the optional heading.</summary>
    public string? Heading { get; internal init; }

    /// <summary>Gets the level of the heading.</summary>
    public SectionLevel Level { get; internal init; }

    /// <summary>Gets the paragraphs, possibly none.</summary>
    public IReadOnlyList<string> Paragraphs { get; internal init; } = [];

    /// <summary>Gets the frequently asked questions, possibly none.</summary>
    public IReadOnlyList<PageQuestion> Questions { get; internal init; } = [];

    /// <summary>Gets the optional person line.</summary>
    public PagePerson<TImageId>? Person { get; internal init; }

    /// <summary>Gets the quotes, possibly none.</summary>
    public IReadOnlyList<PageQuote> Quotes { get; internal init; } = [];

    /// <summary>Gets the application's extension.</summary>
    public TExtra Extra { get; internal init; }

    /// <summary>Gets the band the section sits on.</summary>
    public SectionTone Tone { get; internal init; }

    /// <summary>Gets the background image of an <see cref="SectionTone.Image"/> band.</summary>
    public TImageId? BackgroundImageId { get; internal init; }

    /// <summary>Gets the images, in order, possibly none.</summary>
    public IReadOnlyList<TImageId> ImageIds { get; internal init; } = [];

    /// <summary>Gets how the images are laid out.</summary>
    public SectionImageLayout ImageLayout { get; internal init; }

    /// <summary>Gets the optional video.</summary>
    public PageVideo<TImageId>? Video { get; internal init; }

    /// <summary>Gets the optional button.</summary>
    public PageLink<TLink>? Link { get; internal init; }

    /// <summary>
    /// Lists every image the section shows, in order: the background, the person's photo, the images and the video's poster. An
    /// application refuses to delete them, or to withdraw their eligibility in a culture a published section shows them in.
    /// </summary>
    /// <returns>The image ids.</returns>
    public IEnumerable<TImageId> ImagesShown()
    {
        if (BackgroundImageId is { } background)
        {
            yield return background;
        }

        if (Person?.ImageId is { } photo)
        {
            yield return photo;
        }

        foreach (var image in ImageIds)
        {
            yield return image;
        }

        if (Video is { } video)
        {
            yield return video.PosterImageId;
        }
    }

    /// <summary>Gives the section as a definition that creates or restores the same section; paragraphs are joined with blank lines.</summary>
    /// <returns>The definition.</returns>
    public PageSectionDefinition<TImageId, TLink, TExtra> ToDefinition() => new()
    {
        Eyebrow = Eyebrow,
        Heading = Heading,
        Level = Level,
        Text = Joined(Paragraphs),
        Questions = [.. Questions.Select(question => new PageQuestionDefinition(question.Question, Joined(question.Answer)))],
        Person = Person is { } person ? new PagePersonDefinition<TImageId>(person.ImageId, person.Name, person.Role) : null,
        Quotes = [.. Quotes.Select(quote => new PageQuoteDefinition(quote.Text, quote.Cite))],
        Extra = Extra,
        Tone = Tone,
        BackgroundImageId = BackgroundImageId,
        ImageIds = ImageIds,
        ImageLayout = ImageLayout,
        Video = Video is { } video ? new PageVideoDefinition<TImageId>(video.Address.Value, video.PosterImageId, video.Title) : null,
        Link = Link is { } link ? new PageLinkDefinition<TLink>(link.Text, link.Target, link.Style) : null,
    };

    /// <inheritdoc />
    public bool Equals(PageSection<TImageId, TLink, TExtra>? other) =>
        other is not null
        && string.Equals(Eyebrow, other.Eyebrow, StringComparison.Ordinal)
        && string.Equals(Heading, other.Heading, StringComparison.Ordinal)
        && Level == other.Level
        && Paragraphs.SequenceEqual(other.Paragraphs, StringComparer.Ordinal)
        && Questions.SequenceEqual(other.Questions)
        && Equals(Person, other.Person)
        && Quotes.SequenceEqual(other.Quotes)
        && Extra.Equals(other.Extra)
        && Tone == other.Tone
        && Nullable.Equals(BackgroundImageId, other.BackgroundImageId)
        && ImageIds.SequenceEqual(other.ImageIds)
        && ImageLayout == other.ImageLayout
        && Equals(Video, other.Video)
        && Equals(Link, other.Link);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PageSection<TImageId, TLink, TExtra>);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Heading is null ? 0 : StringComparer.Ordinal.GetHashCode(Heading), Level, Paragraphs.Count, Questions.Count, Quotes.Count, Extra, Tone, ImageIds.Count);

    private static string? Joined(IReadOnlyList<string> paragraphs) => paragraphs.Count == 0 ? null : string.Join("\n\n", paragraphs);
}
