using System.Globalization;
using PandaBear.BuildingBlocks;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.PageSections;

/// <summary>Validates the ordered sections of one translation of a page, and reads the images they show.</summary>
public static class PageSectionList
{
    /// <summary>
    /// Validates the sections of one translation, ViajantesTurismo V2's rules: at most <see cref="PageSectionRules.MaxSections"/>; each
    /// section's parts follow their rules and the section shows more than its heading; a subsection follows a section with a heading, so
    /// the headings never skip a level; a published translation has a section, and once everything else is valid every image its sections
    /// show is eligible in the culture (one error on <see cref="PageSectionFields.Images"/> that counts them). The application's context
    /// validates the button targets and the extension.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TLink">The application's link target.</typeparam>
    /// <typeparam name="TExtra">The application's extension.</typeparam>
    /// <param name="definitions">The entered sections, in order; <see langword="null"/> is none.</param>
    /// <param name="rules">The limits.</param>
    /// <param name="context">The application's images, links and extension.</param>
    /// <param name="culture">The culture of the translation.</param>
    /// <param name="published">Whether the translation is published.</param>
    /// <param name="fieldPrefix">The path of the list, such as <c>pt.sections</c>; each section's errors are under <c>{fieldPrefix}[i]</c>.</param>
    /// <returns>The sections, or every validation error.</returns>
    /// <exception cref="ArgumentNullException">A required argument, a section or an entry of its lists is <see langword="null"/>.</exception>
    public static Result<IReadOnlyList<PageSection<TImageId, TLink, TExtra>>> Create<TImageId, TLink, TExtra>(
        IReadOnlyList<PageSectionDefinition<TImageId, TLink, TExtra>>? definitions,
        PageSectionRules rules,
        IPageSectionContext<TImageId, TLink, TExtra> context,
        ContentCulture culture,
        bool published,
        string fieldPrefix)
        where TImageId : struct, IEquatable<TImageId>
        where TLink : notnull
        where TExtra : struct, IEquatable<TExtra>
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(fieldPrefix);

        definitions ??= [];
        var errors = new ValidationErrors();
        var sections = new List<PageSection<TImageId, TLink, TExtra>>(definitions.Count);
        if (definitions.Count > rules.MaxSections)
        {
            SectionText.Add(errors, fieldPrefix, string.Create(CultureInfo.InvariantCulture, $"A page cannot have more than {rules.MaxSections} sections."));
        }
        else
        {
            var validator = new PageSectionValidator<TImageId, TLink, TExtra>(errors, rules, context, published);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A section is null.");
                sections.Add(validator.Validate(definition, PageSectionFields.Section(fieldPrefix, index)));
            }

            Levels(errors, definitions, fieldPrefix);
        }

        if (published && definitions.Count == 0)
        {
            SectionText.Add(errors, fieldPrefix, "A page needs at least one section before publishing.");
        }

        if (published && !errors.HasErrors && FindIneligible(sections, culture, context).Count is > 0 and var ineligible)
        {
            SectionText.Add(errors, PageSectionFields.Images(culture), string.Create(
                CultureInfo.InvariantCulture, $"Every image must be eligible in this language before publishing; {ineligible} are not."));
        }

        return errors.HasErrors
            ? errors.ToResult<IReadOnlyList<PageSection<TImageId, TLink, TExtra>>>()
            : Result.Ok<IReadOnlyList<PageSection<TImageId, TLink, TExtra>>>(sections.AsReadOnly());
    }

    /// <summary>Lists every image the sections show, each once, in order (see <see cref="PageSection{TImageId, TLink, TExtra}.ImagesShown"/>).</summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TLink">The application's link target.</typeparam>
    /// <typeparam name="TExtra">The application's extension.</typeparam>
    /// <param name="sections">The sections.</param>
    /// <returns>The image ids.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sections"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<TImageId> ImagesShown<TImageId, TLink, TExtra>(IEnumerable<PageSection<TImageId, TLink, TExtra>> sections)
        where TImageId : struct, IEquatable<TImageId>
        where TLink : notnull
        where TExtra : struct, IEquatable<TExtra>
    {
        ArgumentNullException.ThrowIfNull(sections);
        return [.. sections.SelectMany(section => section.ImagesShown()).Distinct()];
    }

    /// <summary>
    /// Lists the images that keep sections from being published in a culture: those that do not exist or are not eligible there, each
    /// once, in order. An editor can name them; the validation error only counts them.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TLink">The application's link target.</typeparam>
    /// <typeparam name="TExtra">The application's extension.</typeparam>
    /// <param name="sections">The sections.</param>
    /// <param name="culture">The culture to publish in.</param>
    /// <param name="images">The application's image knowledge.</param>
    /// <returns>The images that are not eligible.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<TImageId> FindIneligible<TImageId, TLink, TExtra>(
        IEnumerable<PageSection<TImageId, TLink, TExtra>> sections,
        ContentCulture culture,
        IImageEligibility<TImageId> images)
        where TImageId : struct, IEquatable<TImageId>
        where TLink : notnull
        where TExtra : struct, IEquatable<TExtra>
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(images);

        return [.. ImagesShown(sections).Where(image => !images.Exists(image) || !images.IsEligible(image, culture))];
    }

    private static void Levels<TImageId, TLink, TExtra>(ValidationErrors errors, IReadOnlyList<PageSectionDefinition<TImageId, TLink, TExtra>> definitions, string prefix)
        where TImageId : struct, IEquatable<TImageId>
        where TLink : notnull
        where TExtra : struct, IEquatable<TExtra>
    {
        var headed = false;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (definition.Level == SectionLevel.Subsection && !headed)
            {
                SectionText.Add(errors, PageSectionFields.Section(prefix, index) + "." + PageSectionFields.Level, "A subsection must follow a section with a heading.");
            }
            else if (definition.Level == SectionLevel.Section && StringSanitizer.TrySanitize(definition.Heading, out var heading) && !string.IsNullOrEmpty(heading))
            {
                headed = true;
            }
        }
    }
}
