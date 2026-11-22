using PandaBear.BuildingBlocks;
using PandaBear.InputNormalization;

namespace PandaBear.PageSections;

/// <summary>Restores stored <see cref="PageSection{TImageId, TLink, TExtra}"/> values.</summary>
public static class PageSection
{
    /// <summary>
    /// For persistence mappers: rebuilds a section from a stored definition, such as one that
    /// <see cref="PageSection{TImageId, TLink, TExtra}.ToDefinition"/> produced. It is structural only: it refuses what rendering cannot
    /// handle (an undefined level, tone, layout or button style, a question, quote, person, button or video missing a required value,
    /// malformed text) and runs no limits, image or publish rules and no context checks, so never use it for input.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TLink">The application's link target.</typeparam>
    /// <typeparam name="TExtra">The application's extension.</typeparam>
    /// <param name="definition">The stored section.</param>
    /// <returns>The section.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/>, or an entry of its lists, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition breaks one of the structural rules above.</exception>
    public static PageSection<TImageId, TLink, TExtra> Restore<TImageId, TLink, TExtra>(PageSectionDefinition<TImageId, TLink, TExtra> definition)
        where TImageId : struct, IEquatable<TImageId>
        where TLink : notnull
        where TExtra : struct, IEquatable<TExtra>
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(definition.Level) || !Enum.IsDefined(definition.Tone) || !Enum.IsDefined(definition.ImageLayout))
        {
            throw Refused("The level, tone or image layout is not defined.");
        }

        return new PageSection<TImageId, TLink, TExtra>
        {
            Eyebrow = Optional(definition.Eyebrow),
            Heading = Optional(definition.Heading),
            Level = definition.Level,
            Paragraphs = Paragraphs(definition.Text),
            Questions = [.. (definition.Questions ?? []).Select(Question)],
            Person = definition.Person is { } person ? new PagePerson<TImageId>(person.ImageId, Required(person.Name), Optional(person.Role)) : null,
            Quotes = [.. (definition.Quotes ?? []).Select(Quote)],
            Extra = definition.Extra,
            Tone = definition.Tone,
            BackgroundImageId = definition.BackgroundImageId,
            ImageIds = [.. definition.ImageIds ?? []],
            ImageLayout = definition.ImageLayout,
            Video = definition.Video is { } video ? Video(video) : null,
            Link = definition.Link is { } link ? Link(link) : null,
        };
    }

    private static PageQuestion Question(PageQuestionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var answer = Paragraphs(definition.Answer);
        return answer.Count > 0 ? new PageQuestion(Required(definition.Question), answer) : throw Refused("A question has no answer.");
    }

    private static PageQuote Quote(PageQuoteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new PageQuote(Required(definition.Text), Optional(definition.Cite));
    }

    private static PageVideo<TImageId> Video<TImageId>(PageVideoDefinition<TImageId> definition)
        where TImageId : struct, IEquatable<TImageId> =>
        definition.PosterImageId is { } poster
            ? new PageVideo<TImageId>(WebUrl.Restore(Required(definition.Address)), poster, Required(definition.Title))
            : throw Refused("A video has no poster.");

    private static PageLink<TLink> Link<TLink>(PageLinkDefinition<TLink> definition)
        where TLink : notnull =>
        Enum.IsDefined(definition.Style) && definition.Target is not null
            ? new PageLink<TLink>(Required(definition.Text), definition.Target, definition.Style)
            : throw Refused("A button's style is not defined, or it has no target.");

    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw Refused("A required text is missing.") : value;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> Paragraphs(string? text) =>
        StringSanitizer.TrySanitizeParagraphs(text, out var paragraphs) ? paragraphs : throw Refused("A text is not valid UTF-16.");

    private static ArgumentException Refused(string message) => new(message);
}
