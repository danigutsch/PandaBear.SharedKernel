using System.Collections.ObjectModel;
using SharedKernel.BuildingBlocks;
using SharedKernel.InputNormalization;

namespace SharedKernel.Offerings;

/// <summary>Rebuilds stored offerings: structural checks only, no lengths, limits or publish rules.</summary>
internal static class OfferingRestoration
{
    /// <summary>Rebuilds an offering's data from a stored definition.</summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="definition">The stored definition.</param>
    /// <param name="rules">The rules, read only for their price-kind flags.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException">The schedule, a list entry or a translation is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition breaks a structural rule.</exception>
    public static OfferingData<TImageId, TPriceKind, TMetric> Restore<TImageId, TPriceKind, TMetric>(
        OfferingDefinition<TImageId, TPriceKind, TMetric> definition,
        OfferingRules<TPriceKind, TMetric> rules)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(definition.Schedule, nameof(definition));

        var currency = CurrencyOf(definition.Currency);
        var steps = new List<ProgramStep<TImageId, TMetric>>();
        foreach (var step in definition.Steps ?? [])
        {
            ArgumentNullException.ThrowIfNull(step, nameof(definition));
            steps.Add(new ProgramStep<TImageId, TMetric>(step.ImageId, Metrics(step.Metrics)));
        }

        return new OfferingData<TImageId, TPriceKind, TMetric>
        {
            SortOrder = definition.SortOrder,
            Schedule = Schedule(definition.Schedule),
            SignUpLink = string.IsNullOrWhiteSpace(definition.SignUpLink) ? null : WebUrl.Restore(definition.SignUpLink),
            Currency = currency,
            Prices = Prices(definition.Prices, currency),
            Participants = definition.Participants is { Min: null, Max: null } ? null : definition.Participants,
            GroupCondition = GroupConditionOf(definition.GroupCondition, currency),
            PaymentTerms = definition.PaymentTerms is { DepositPercent: null, Instalments: null, BalanceDueDays: null } ? null : definition.PaymentTerms,
            Metrics = Metrics(definition.Metrics),
            Steps = steps.AsReadOnly(),
            CoverId = definition.CoverId,
            GalleryIds = [.. definition.GalleryIds ?? []],
            Translations = Translations(definition.Translations),
            PriceKinds = rules.PriceKinds,
        };
    }

    private static OfferingSchedule Schedule(OfferingScheduleDefinition schedule)
    {
        if (!OfferingSchedules.IsValid(schedule.Availability))
        {
            throw Refused("The availability is not set or not defined.");
        }

        DateOnlyRange? dates = null;
        if (schedule.StartDate is not null || schedule.EndDate is not null)
        {
            if (schedule.StartDate is not { } start || schedule.EndDate is not { } end || DateOnlyRange.Create(start, end) is not { IsSuccess: true } range)
            {
                throw Refused("The dates are not a range of days.");
            }

            dates = range.Value;
        }

        if (dates is null && schedule.Availability is OfferingAvailability.Scheduled or OfferingAvailability.SoldOut)
        {
            throw Refused("A scheduled or sold-out offering has no dates.");
        }

        if (dates is null && schedule.Availability == OfferingAvailability.DatesToBeConfirmed && schedule.Year is null)
        {
            throw Refused("An undated offering whose dates are to be confirmed has no year.");
        }

        return schedule.DurationDays is { } days && days >= 1
            ? new OfferingSchedule(schedule.Availability, dates, dates?.StartDate.Year ?? schedule.Year, days)
            : throw Refused("The duration is missing or not positive.");
    }

    private static Currency? CurrencyOf(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        return trimmed.Length == 3 && trimmed.All(char.IsAsciiLetter) ? Currency.Restore(trimmed) : throw Refused("The currency code is malformed.");
    }

    private static ReadOnlyCollection<PriceLine<TPriceKind>> Prices<TPriceKind>(IReadOnlyList<PriceLineDefinition<TPriceKind>>? definitions, Currency? currency)
        where TPriceKind : struct, Enum
    {
        var lines = new List<PriceLine<TPriceKind>>();
        foreach (var definition in definitions ?? [])
        {
            ArgumentNullException.ThrowIfNull(definition, nameof(definitions));
            if (!Enum.IsDefined(definition.Kind))
            {
                throw Refused("A price kind is not defined.");
            }

            lines.Add(new PriceLine<TPriceKind>(definition.Kind, Money.Restore(definition.Amount, currency ?? throw Refused("Prices need a currency."))));
        }

        return lines.AsReadOnly();
    }

    private static GroupCondition? GroupConditionOf(GroupConditionDefinition? definition, Currency? currency)
    {
        if (definition is null || (definition.MinimumParticipants is null && definition.SurchargePerMissing is null && definition.DueDays is null))
        {
            return null;
        }

        return definition is { MinimumParticipants: { } minimum, SurchargePerMissing: { } surcharge, DueDays: { } dueDays }
            ? new GroupCondition(minimum, Money.Restore(surcharge, currency ?? throw Refused("A group condition needs a currency.")), dueDays)
            : throw Refused("A group condition needs all three values.");
    }

    private static ReadOnlyCollection<Metric<TMetric>> Metrics<TMetric>(IReadOnlyList<Metric<TMetric>>? metrics)
        where TMetric : struct, Enum
    {
        var restored = new List<Metric<TMetric>>();
        foreach (var metric in metrics ?? [])
        {
            ArgumentNullException.ThrowIfNull(metric, nameof(metrics));
            restored.Add(Enum.IsDefined(metric.Kind) ? metric : throw Refused("A metric kind is not defined."));
        }

        return restored.AsReadOnly();
    }

    private static ReadOnlyCollection<OfferingTranslation> Translations(IReadOnlyList<OfferingTranslationDefinition>? definitions)
    {
        var translations = new List<OfferingTranslation>();
        var cultures = new HashSet<ContentCulture>();
        foreach (var definition in definitions ?? [])
        {
            ArgumentNullException.ThrowIfNull(definition, nameof(definitions));
            var culture = ContentCulture.Create(definition.Culture);
            if (culture.IsFailure || !cultures.Add(culture.Value))
            {
                throw Refused("A translation's culture is malformed or given twice.");
            }

            translations.Add(Translation(culture.Value, definition));
        }

        return translations.AsReadOnly();
    }

    private static OfferingTranslation Translation(ContentCulture culture, OfferingTranslationDefinition definition)
    {
        if (!Enum.IsDefined(definition.Status))
        {
            throw Refused("A translation's status is not defined.");
        }

        if (!Slug.IsCanonical(definition.Slug, int.MaxValue))
        {
            throw Refused("A translation's slug is not canonical.");
        }

        return new OfferingTranslation(culture, definition.Status, Slug.Restore(definition.Slug!), Required(definition.Title))
        {
            Subtitle = Optional(definition.Subtitle),
            Eyebrow = Optional(definition.Eyebrow),
            Summary = Optional(definition.Summary),
            Description = Paragraphs(definition.Description),
            DateNote = Optional(definition.DateNote),
            SeoTitle = Optional(definition.SeoTitle),
            SeoDescription = Optional(definition.SeoDescription),
            Highlights = Lines(definition.Highlights),
            Included = Lines(definition.Included),
            NotIncluded = Lines(definition.NotIncluded),
            PracticalInfo = Lines(definition.PracticalInfo),
            PriceLabels = Labels(definition.PriceLabels),
            StepTexts = [.. (definition.StepTexts ?? []).Select(StepText)],
        };
    }

    private static ProgramStepText StepText(ProgramStepTextDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ProgramStepText(Required(definition.Title), Paragraphs(definition.Text), Optional(definition.Note));
    }

    private static string Required(string? value) =>
        string.IsNullOrWhiteSpace(value) ? throw Refused("A required text is missing.") : value;

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> Lines(IReadOnlyList<string?>? values) => [.. (values ?? []).Select(Required)];

    private static ReadOnlyCollection<string?> Labels(IReadOnlyList<string?>? values)
    {
        var labels = (values ?? []).Select(Optional).ToList();
        while (labels.Count > 0 && labels[^1] is null)
        {
            labels.RemoveAt(labels.Count - 1);
        }

        return labels.AsReadOnly();
    }

    private static IReadOnlyList<string> Paragraphs(string? text) =>
        StringSanitizer.TrySanitizeParagraphs(text, out var paragraphs) ? paragraphs : throw Refused("A text is not valid UTF-16.");

    private static ArgumentException Refused(string message) => new(message);
}
