using System.Collections.ObjectModel;
using System.Globalization;
using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.PageSections;

/// <summary>Validates one section, ViajantesTurismo V2's rules. Messages never echo the input.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLinkInput">The application's raw link target.</typeparam>
/// <typeparam name="TLink">The application's validated link target.</typeparam>
/// <typeparam name="TExtra">The application's extension.</typeparam>
/// <param name="errors">Collects the errors.</param>
/// <param name="rules">The limits.</param>
/// <param name="context">The application's context.</param>
/// <param name="published">Whether the translation is published.</param>
internal sealed class PageSectionValidator<TImageId, TLinkInput, TLink, TExtra>(
    ValidationErrors errors,
    PageSectionRules rules,
    IPageSectionContext<TImageId, TLinkInput, TLink, TExtra> context,
    bool published)
    where TImageId : struct, IEquatable<TImageId>
    where TLinkInput : class
    where TLink : notnull
    where TExtra : struct, IEquatable<TExtra>
{
    private const string Content = "A section needs text, questions, a person, a quote, images, a video, a button or other content.";

    /// <summary>Validates a section.</summary>
    /// <param name="definition">The entered section.</param>
    /// <param name="field">The section's path.</param>
    /// <returns>The section; meaningful only when no error was added.</returns>
    /// <exception cref="ArgumentNullException">An entry of a list in the definition is <see langword="null"/>.</exception>
    public PageSection<TImageId, TLink, TExtra> Validate(PageSectionDefinition<TImageId, TLinkInput, TExtra> definition, string field)
    {
        var eyebrow = SanitizedText.Optional(errors, definition.Eyebrow, Part(field, PageSectionFields.Eyebrow), "Section eyebrow", rules.MaxLineLength);
        var heading = SanitizedText.Optional(errors, definition.Heading, Part(field, PageSectionFields.Heading), "Section heading", rules.MaxTitleLength, out var hasHeading);
        Level(definition.Level, hasHeading, field);
        var paragraphs = SanitizedText.Paragraphs(errors, definition.Text, Part(field, PageSectionFields.Text), "Section text", rules.MaxParagraphs, rules.MaxParagraphLength, out var hasText);
        var questions = Questions(definition.Questions ?? [], Part(field, PageSectionFields.Questions));
        var extra = context.ValidateExtra(definition.Extra, published, field);
        errors.AddIfFailed(extra);
        Tone(definition.Tone, definition.BackgroundImageId, field);
        var person = definition.Person is { } entered ? Person(entered, Part(field, PageSectionFields.Person)) : null;
        var quotes = Quotes(definition.Quotes ?? [], Part(field, PageSectionFields.Quotes));
        var imageIds = definition.ImageIds ?? [];
        Images(imageIds, Part(field, PageSectionFields.ImageIds));
        if (!Enum.IsDefined(definition.ImageLayout))
        {
            SectionText.Add(errors, Part(field, PageSectionFields.ImageLayout), "The image layout is not supported.");
        }

        var video = definition.Video is { } enteredVideo ? Video(enteredVideo, Part(field, PageSectionFields.Video)) : null;
        var link = definition.Link is { } enteredLink ? Link(enteredLink, Part(field, PageSectionFields.Link)) : null;
        if (!hasText && (definition.Questions ?? []).Count == 0 && !context.CountsAsContent(definition.Extra) && definition.Person is null
            && quotes.Count == 0 && imageIds.Count == 0 && definition.Video is null && definition.Link is null)
        {
            SectionText.Add(errors, field, Content);
        }

        return new PageSection<TImageId, TLink, TExtra>
        {
            Eyebrow = eyebrow,
            Heading = heading,
            Level = definition.Level,
            Paragraphs = paragraphs,
            Questions = questions,
            Person = person,
            Quotes = quotes,
            Extra = extra.IsSuccess ? extra.Value : definition.Extra,
            Tone = definition.Tone,
            BackgroundImageId = definition.BackgroundImageId,
            ImageIds = [.. imageIds],
            ImageLayout = definition.ImageLayout,
            Video = video,
            Link = link,
        };
    }

    private static string Part(string field, string part) => field + "." + part;

    private static string Indexed(string field, int index, string part) => string.Create(CultureInfo.InvariantCulture, $"{field}[{index}].{part}");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private void Level(SectionLevel level, bool hasHeading, string field)
    {
        if (!Enum.IsDefined(level))
        {
            SectionText.Add(errors, Part(field, PageSectionFields.Level), "The heading level is not supported.");
        }
        else if (level == SectionLevel.Subsection && !hasHeading)
        {
            SectionText.Add(errors, Part(field, PageSectionFields.Heading), "A subsection needs a heading.");
        }
    }

    private ReadOnlyCollection<PageQuestion> Questions(IReadOnlyList<PageQuestionDefinition> definitions, string field)
    {
        if (definitions.Count > rules.MaxQuestions)
        {
            SectionText.Add(errors, field, Invariant($"A section cannot have more than {rules.MaxQuestions} questions."));
            return ReadOnlyCollection<PageQuestion>.Empty;
        }

        var questions = new List<PageQuestion>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A question is null.");
            var number = index + 1;
            var question = SanitizedText.Required(errors, definition.Question, Indexed(field, index, PageSectionFields.Question), Invariant($"Question {number}"), rules.MaxLineLength) ?? string.Empty;
            var answerField = Indexed(field, index, PageSectionFields.Answer);
            var answer = SanitizedText.Paragraphs(errors, definition.Answer, answerField, Invariant($"Question {number} answer"), rules.MaxParagraphs, rules.MaxParagraphLength, out var hasAnswer);
            if (!hasAnswer)
            {
                SectionText.Add(errors, answerField, Invariant($"Question {number} answer is required."));
            }

            questions.Add(new PageQuestion(question, answer));
        }

        return questions.AsReadOnly();
    }

    private void Tone(SectionTone tone, TImageId? backgroundImageId, string field)
    {
        var backgroundField = Part(field, PageSectionFields.BackgroundImageId);
        if (!Enum.IsDefined(tone))
        {
            SectionText.Add(errors, Part(field, PageSectionFields.Tone), "The section tone is not supported.");
        }
        else if (tone == SectionTone.Image && backgroundImageId is null)
        {
            SectionText.Add(errors, backgroundField, "A section on an image band needs a background image.");
        }
        else if (tone != SectionTone.Image && backgroundImageId is not null)
        {
            SectionText.Add(errors, backgroundField, "Only a section on an image band has a background image.");
        }
        else if (backgroundImageId is { } background && !context.Exists(background))
        {
            SectionText.Add(errors, backgroundField, "The background image does not exist.");
        }
    }

    private PagePerson<TImageId> Person(PagePersonDefinition<TImageId> definition, string field)
    {
        var name = SanitizedText.Required(errors, definition.Name, Part(field, PageSectionFields.Name), "Person name", rules.MaxTitleLength) ?? string.Empty;
        var role = SanitizedText.Optional(errors, definition.Role, Part(field, PageSectionFields.Role), "Person role", rules.MaxTitleLength);
        if (definition.ImageId is { } photo && !context.Exists(photo))
        {
            SectionText.Add(errors, Part(field, PageSectionFields.ImageId), "The person's photo does not exist.");
        }

        return new PagePerson<TImageId>(definition.ImageId, name, role);
    }

    private ReadOnlyCollection<PageQuote> Quotes(IReadOnlyList<PageQuoteDefinition> definitions, string field)
    {
        if (definitions.Count > rules.MaxQuotes)
        {
            SectionText.Add(errors, field, Invariant($"A section cannot have more than {rules.MaxQuotes} quotes."));
            return ReadOnlyCollection<PageQuote>.Empty;
        }

        var quotes = new List<PageQuote>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A quote is null.");
            var number = index + 1;
            var text = SanitizedText.Required(errors, definition.Text, Indexed(field, index, PageSectionFields.QuoteText), Invariant($"Quote {number} text"), rules.MaxQuoteLength) ?? string.Empty;
            var cite = SanitizedText.Optional(errors, definition.Cite, Indexed(field, index, PageSectionFields.Cite), Invariant($"Quote {number} source"), rules.MaxTitleLength);
            quotes.Add(new PageQuote(text, cite));
        }

        return quotes.AsReadOnly();
    }

    private void Images(IReadOnlyList<TImageId> imageIds, string field)
    {
        if (imageIds.Count > rules.MaxImages)
        {
            SectionText.Add(errors, field, Invariant($"A section cannot show more than {rules.MaxImages} images."));
        }
        else if (imageIds.Any(image => !context.Exists(image)))
        {
            SectionText.Add(errors, field, "An image of the section does not exist.");
        }
        else if (imageIds.Distinct().Count() != imageIds.Count)
        {
            SectionText.Add(errors, field, "An image of the section appears twice.");
        }
    }

    private PageVideo<TImageId>? Video(PageVideoDefinition<TImageId> definition, string field)
    {
        var address = WebUrl.CreateHttps(definition.Address, Part(field, PageSectionFields.Address), "Video address");
        errors.AddIfFailed(address);
        var posterField = Part(field, PageSectionFields.PosterImageId);
        if (definition.PosterImageId is not { } poster || poster.Equals(default))
        {
            SectionText.Add(errors, posterField, "Choose a poster image for the video.");
        }
        else if (!context.Exists(poster))
        {
            SectionText.Add(errors, posterField, "The poster image does not exist.");
        }

        var title = SanitizedText.Required(errors, definition.Title, Part(field, PageSectionFields.Title), "Video title", rules.MaxTitleLength) ?? string.Empty;
        return address.IsSuccess && definition.PosterImageId is { } id ? new PageVideo<TImageId>(address.Value, id, title) : null;
    }

    private PageLink<TLink>? Link(PageLinkDefinition<TLinkInput> definition, string field)
    {
        var text = SanitizedText.Required(errors, definition.Text, Part(field, PageSectionFields.LinkText), "Button text", rules.MaxTitleLength) ?? string.Empty;
        if (!Enum.IsDefined(definition.Style))
        {
            SectionText.Add(errors, Part(field, PageSectionFields.Style), "The button style is not supported.");
        }

        if (definition.Target is not { } input)
        {
            SectionText.Add(errors, field, "Choose where the button links.");
            return null;
        }

        var target = context.ValidateLink(input, field);
        errors.AddIfFailed(target);
        return target.IsSuccess ? new PageLink<TLink>(text, target.Value, definition.Style) : null;
    }
}
