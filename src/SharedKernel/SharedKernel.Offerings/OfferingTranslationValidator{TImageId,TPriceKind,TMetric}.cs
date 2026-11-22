using System.Collections.ObjectModel;
using System.Globalization;
using PandaBear.BuildingBlocks;
using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.Offerings;

/// <summary>Validates the text of an offering in one culture, and its publish rule.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
/// <param name="errors">Collects the errors.</param>
/// <param name="rules">The rules.</param>
/// <param name="images">The image knowledge.</param>
/// <param name="culture">The culture of the translation.</param>
internal sealed class OfferingTranslationValidator<TImageId, TPriceKind, TMetric>(
    ValidationErrors errors,
    OfferingRules<TPriceKind, TMetric> rules,
    IImageEligibility<TImageId> images,
    ContentCulture culture)
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    private readonly ValidationErrors _errors = errors;
    private readonly OfferingRules<TPriceKind, TMetric> _rules = rules;
    private readonly IImageEligibility<TImageId> _images = images;
    private readonly ContentCulture _culture = culture;

    /// <summary>Validates a translation.</summary>
    /// <param name="definition">The entered text.</param>
    /// <param name="dated">Whether the offering has dates.</param>
    /// <param name="labelOrder">For each labelled price in the sorted order, its position among the labelled prices as entered.</param>
    /// <param name="stepCount">How many steps the offering has.</param>
    /// <param name="imageIds">Every image the offering shows.</param>
    /// <returns>The translation, or <see langword="null"/> when its title or slug is refused.</returns>
    public OfferingTranslation? Validate(OfferingTranslationDefinition definition, bool dated, IReadOnlyList<int> labelOrder, int stepCount, IReadOnlyList<TImageId> imageIds)
    {
        var title = TextInput.Required(_errors, definition.Title, Field(OfferingFields.Title), "Title", _rules.MaxTitleLength);
        var slug = SlugOf(definition.Slug, title);
        if (!Enum.IsDefined(definition.Status))
        {
            Add(OfferingFields.Status, "The publication status is not valid.");
        }

        var dateNote = TextInput.Optional(_errors, definition.DateNote, Field(OfferingFields.DateNote), "Date note", _rules.MaxDateNoteLength);
        if (dateNote is not null && dated)
        {
            Add(OfferingFields.DateNote, "A date note is allowed only when the offering has no dates.");
        }

        var summary = TextInput.Optional(_errors, definition.Summary, Field(OfferingFields.Summary), "Summary", _rules.MaxSummaryLength);
        var description = TextInput.Paragraphs(
            _errors, definition.Description, Field(OfferingFields.Description), "Description", _rules.MaxParagraphs, _rules.MaxParagraphLength);
        var highlights = Lines(definition.Highlights, OfferingFields.Highlights, "Highlights");
        if (_rules.HighlightsCount is { } range && highlights.Count > 0 && (highlights.Count < range.Min || highlights.Count > range.Max))
        {
            Add(OfferingFields.Highlights, Invariant($"Give no highlights, or between {range.Min} and {range.Max}."));
        }

        var labels = Labels(definition.PriceLabels, labelOrder);

        var stepTexts = StepTexts(definition.StepTexts ?? [], stepCount);
        if (definition.Status == PublicationStatus.Published)
        {
            PublishRule(summary, description, labels.Count == labelOrder.Count && labels.All(label => label is not null), stepTexts.Count == stepCount, imageIds);
        }

        return title is null || slug is null
            ? null
            : new OfferingTranslation(_culture, definition.Status, slug, title)
            {
                Subtitle = TextInput.Optional(_errors, definition.Subtitle, Field(OfferingFields.Subtitle), "Subtitle", _rules.MaxLineLength),
                Eyebrow = TextInput.Optional(_errors, definition.Eyebrow, Field(OfferingFields.Eyebrow), "Eyebrow", _rules.MaxLineLength),
                Summary = summary,
                Description = description,
                DateNote = dateNote,
                SeoTitle = TextInput.Optional(_errors, definition.SeoTitle, Field(OfferingFields.SeoTitle), "SEO title", _rules.MaxTitleLength),
                SeoDescription = TextInput.Optional(_errors, definition.SeoDescription, Field(OfferingFields.SeoDescription), "SEO description", _rules.MaxSummaryLength),
                Highlights = highlights,
                Included = Lines(definition.Included, OfferingFields.Included, "Included"),
                NotIncluded = Lines(definition.NotIncluded, OfferingFields.NotIncluded, "Not included"),
                PracticalInfo = Lines(definition.PracticalInfo, OfferingFields.PracticalInfo, "Practical information"),
                PriceLabels = labels,
                StepTexts = stepTexts,
            };
    }

    private Slug? SlugOf(string? value, string? title)
    {
        if (string.IsNullOrWhiteSpace(value) && title is null)
        {
            return null;
        }

        var field = Field(OfferingFields.Slug);
        var slug = string.IsNullOrWhiteSpace(value)
            ? Slug.CreateLenient(title, _rules.MaxSlugLength, field)
            : Slug.Create(value, _rules.MaxSlugLength, field);
        _errors.AddIfFailed(slug);
        return slug.IsSuccess ? slug.Value : null;
    }

    /// <summary>
    /// Validates the labels, entered in the entered order of the labelled prices with blank for a missing one, and reorders them with
    /// their prices. Missing labels stay as <see langword="null"/> in their place; trailing ones are left out.
    /// </summary>
    private ReadOnlyCollection<string?> Labels(IReadOnlyList<string?>? values, IReadOnlyList<int> labelOrder)
    {
        var field = Field(OfferingFields.PriceLabels);
        var entered = new List<string?>();
        foreach (var value in values ?? [])
        {
            if (!StringSanitizer.TrySanitize(value, out var sanitized))
            {
                Add(OfferingFields.PriceLabels, "Price labels is not valid text.");
                return ReadOnlyCollection<string?>.Empty;
            }

            if (sanitized?.Length > _rules.MaxTitleLength)
            {
                Add(OfferingFields.PriceLabels, Invariant($"Price labels: each label must be at most {_rules.MaxTitleLength} characters."));
                return ReadOnlyCollection<string?>.Empty;
            }

            entered.Add(string.IsNullOrEmpty(sanitized) ? null : sanitized);
        }

        if (entered.Count > labelOrder.Count)
        {
            Add(OfferingFields.PriceLabels, "There are more price labels than prices that need one.");
            return ReadOnlyCollection<string?>.Empty;
        }

        var sorted = labelOrder.Select(position => position < entered.Count ? entered[position] : null).ToList();
        while (sorted.Count > 0 && sorted[^1] is null)
        {
            sorted.RemoveAt(sorted.Count - 1);
        }

        return sorted.AsReadOnly();
    }

    private IReadOnlyList<string> Lines(IReadOnlyList<string?>? values, string field, string label) =>
        TextInput.Lines(_errors, values, Field(field), label, _rules.MaxListItems, _rules.MaxLineLength);

    private ReadOnlyCollection<ProgramStepText> StepTexts(IReadOnlyList<ProgramStepTextDefinition> definitions, int stepCount)
    {
        if (definitions.Count > stepCount)
        {
            Add(OfferingFields.Steps, "There are more step texts than steps.");
        }

        var texts = new List<ProgramStepText>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A step text is null.");
            var number = index + 1;
            var title = TextInput.Required(_errors, definition.Title, Field(OfferingFields.StepTitle(index)), Invariant($"Step {number} title"), _rules.MaxTitleLength);
            if (title is not null && _rules.ForbiddenStepTitlePrefixes.Any(prefix => StartsWithNumbered(title, prefix)))
            {
                Add(OfferingFields.StepTitle(index), Invariant($"Step {number} title must not start with a day number; the number comes from its position."));
            }

            var paragraphs = TextInput.Paragraphs(
                _errors, definition.Text, Field(OfferingFields.StepText(index)), Invariant($"Step {number} text"), _rules.MaxParagraphs, _rules.MaxParagraphLength);
            var note = TextInput.Optional(_errors, definition.Note, Field(OfferingFields.StepNote(index)), Invariant($"Step {number} note"), _rules.MaxLineLength);
            texts.Add(new ProgramStepText(title ?? string.Empty, paragraphs, note));
        }

        return texts.AsReadOnly();
    }

    private static bool StartsWithNumbered(string title, string prefix)
    {
        if (!title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = title.AsSpan(prefix.Length).TrimStart();
        return rest.Length > 0 && char.IsAsciiDigit(rest[0]);
    }

    private void PublishRule(string? summary, IReadOnlyList<string> description, bool labelsComplete, bool stepsComplete, IReadOnlyList<TImageId> imageIds)
    {
        if (summary is null)
        {
            Add(OfferingFields.Summary, "A summary is required before publishing.");
        }

        if (description.Count == 0)
        {
            Add(OfferingFields.Description, "A description is required before publishing.");
        }

        if (!labelsComplete)
        {
            Add(OfferingFields.PriceLabels, "Every price of a kind that needs a label needs one in this language before publishing.");
        }

        if (!stepsComplete)
        {
            Add(OfferingFields.Steps, "Every step needs its text in this language before publishing.");
        }

        var ineligible = OfferingPublishing.FindIneligible(imageIds, _culture, _images).Count;
        if (ineligible > 0)
        {
            Add(OfferingFields.Images, Invariant($"Every image must be eligible in this language before publishing; {ineligible} are not."));
        }
    }

    private string Field(string field) => OfferingFields.Of(_culture, field);

    private void Add(string field, string message) => TextInput.Add(_errors, Field(field), message);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
