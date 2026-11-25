using System.Collections.ObjectModel;
using System.Globalization;
using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Offerings;

/// <summary>Validates an offering definition into offering data, collecting every error. Messages never echo the input.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
/// <param name="rules">The rules.</param>
/// <param name="images">The image knowledge.</param>
internal sealed class OfferingValidator<TImageId, TPriceKind, TMetric>(OfferingRules<TPriceKind, TMetric> rules, IImageEligibility<TImageId> images)
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    private const int MaxDueDays = 365;
    private const string GroupConditionTogether = "Give the minimum participants, the surcharge per missing participant and the due days together.";

    private readonly ValidationErrors _errors = new();
    private readonly OfferingRules<TPriceKind, TMetric> _rules = rules;
    private readonly IImageEligibility<TImageId> _images = images;

    /// <summary>Validates a definition. The schedule is validated first, so it is built only when it has no error.</summary>
    /// <param name="definition">The entered data.</param>
    /// <returns>The data, or every error.</returns>
    /// <exception cref="ArgumentNullException">The schedule, a list entry or a translation is <see langword="null"/>.</exception>
    public Result<OfferingData<TImageId, TPriceKind, TMetric>> Validate(OfferingDefinition<TImageId, TPriceKind, TMetric> definition)
    {
        ArgumentNullException.ThrowIfNull(definition.Schedule, nameof(definition));

        var dated = definition.Schedule.StartDate is not null || definition.Schedule.EndDate is not null;
        var schedule = Schedule(definition.Schedule, dated);
        var signUpLink = SignUpLink(definition.SignUpLink);
        var prices = definition.Prices ?? [];
        var groupGiven = definition.GroupCondition is { } group
            && (group.MinimumParticipants is not null || group.SurchargePerMissing is not null || group.DueDays is not null);
        var currency = CurrencyOf(definition.Currency, prices.Count > 0 || groupGiven);
        var lines = Prices(prices, currency, out var labelOrder);
        var participants = Participants(definition.Participants);
        var groupCondition = groupGiven ? GroupConditionOf(definition.GroupCondition!, definition.Participants, currency) : null;
        var paymentTerms = PaymentTermsOf(definition.PaymentTerms);
        var metrics = Metrics(definition.Metrics ?? [], MetricScope.Offering, OfferingFields.Metrics);
        var steps = Steps(definition.Steps ?? [], definition.Schedule.DurationDays);
        var galleryIds = Images(definition.CoverId, definition.GalleryIds ?? []);
        var imageIds = AllImages(definition.CoverId, galleryIds, steps);
        var translations = Translations(definition.Translations ?? [], dated, labelOrder, steps.Count, imageIds);

        if (_errors.HasErrors)
        {
            return _errors.ToResult<OfferingData<TImageId, TPriceKind, TMetric>>();
        }

        return Result.Ok(new OfferingData<TImageId, TPriceKind, TMetric>
        {
            SortOrder = definition.SortOrder,
            Schedule = schedule!,
            SignUpLink = signUpLink,
            Currency = currency,
            Prices = lines,
            Participants = participants,
            GroupCondition = groupCondition,
            PaymentTerms = paymentTerms,
            Metrics = metrics,
            Steps = steps,
            CoverId = definition.CoverId,
            GalleryIds = galleryIds,
            Translations = translations,
            PriceKinds = _rules.PriceKinds,
        });
    }

    private OfferingSchedule? Schedule(OfferingScheduleDefinition definition, bool dated)
    {
        var availability = definition.Availability;
        var defined = availability != OfferingAvailability.Unspecified && Enum.IsDefined(availability);
        if (!defined)
        {
            Add(OfferingFields.Availability, availability == OfferingAvailability.Unspecified ? "Availability is required." : "The availability is not supported.");
        }

        var dates = Dates(definition);
        if (defined)
        {
            AvailabilityRules(availability, dated, definition.Year);
        }

        if (dated && definition.Year is not null)
        {
            Add(OfferingFields.Year, "The year comes from the dates; leave it empty.");
        }
        else if (!dated && availability != OfferingAvailability.OnRequest && definition.Year is { } year && !InYears(year))
        {
            Add(OfferingFields.Year, Invariant($"Year must be between {_rules.MinYear} and {_rules.MaxYear}."));
        }

        Duration(definition.DurationDays, dates);

        return !_errors.HasErrors && definition.DurationDays is { } days
            ? new OfferingSchedule(availability, dates, dates?.StartDate.Year ?? definition.Year, days)
            : null;
    }

    private DateOnlyRange? Dates(OfferingScheduleDefinition definition)
    {
        if (definition.StartDate is { } start && definition.EndDate is { } end)
        {
            var range = DateOnlyRange.Create(start, end);
            if (range.IsFailure)
            {
                _errors.Add(range);
                return null;
            }

            if (!InYears(start.Year) || !InYears(end.Year))
            {
                Add(OfferingFields.StartDate, Invariant($"The dates must be between {_rules.MinYear} and {_rules.MaxYear}."));
            }

            return range.Value;
        }

        if (definition.StartDate is not null)
        {
            Add(OfferingFields.EndDate, "End date is required when a start date is given.");
        }
        else if (definition.EndDate is not null)
        {
            Add(OfferingFields.StartDate, "Start date is required when an end date is given.");
        }

        return null;
    }

    private void AvailabilityRules(OfferingAvailability availability, bool dated, int? year)
    {
        switch (availability)
        {
            case OfferingAvailability.Scheduled or OfferingAvailability.SoldOut:
                if (!dated)
                {
                    Add(OfferingFields.StartDate, "A scheduled or sold-out offering needs its dates.");
                }

                break;
            case OfferingAvailability.OnRequest:
                if (dated)
                {
                    Add(OfferingFields.StartDate, "An offering on request has no dates.");
                }

                if (year is not null)
                {
                    Add(OfferingFields.Year, "An offering on request has no year.");
                }

                break;
            case OfferingAvailability.DatesToBeConfirmed:
                if (!dated && year is null)
                {
                    Add(OfferingFields.Year, "An offering whose dates are to be confirmed needs a year until it has dates.");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(availability), availability, "Only a valid availability has date rules.");
        }
    }

    private void Duration(int? durationDays, DateOnlyRange? dates)
    {
        if (durationDays is not { } days)
        {
            Add(OfferingFields.DurationDays, "Duration is required.");
        }
        else if (days < 1 || days > _rules.MaxDurationDays)
        {
            Add(OfferingFields.DurationDays, Invariant($"Duration must be between 1 and {_rules.MaxDurationDays} days."));
        }
        else if (dates is not null && days != dates.Days)
        {
            Add(OfferingFields.DurationDays, "Duration must equal the number of days from the start to the end date, both included.");
        }
    }

    private WebUrl? SignUpLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var link = WebUrl.Create(value, OfferingFields.SignUpLink, "Sign-up link");
        _errors.AddIfFailed(link);
        return link.IsSuccess ? link.Value : null;
    }

    private Currency? CurrencyOf(string? code, bool required)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            if (required)
            {
                Add(OfferingFields.Currency, "Currency is required with prices or a group condition.");
            }

            return null;
        }

        var currency = Currency.Create(code);
        _errors.AddIfFailed(currency);
        return currency.IsSuccess ? currency.Value : null;
    }

    /// <summary>
    /// Validates the prices and sorts them into the kind order. A translation's labels are entered in the entered order of the labelled
    /// prices, so <paramref name="labelOrder"/> gives, for each labelled price in the sorted order, its position among the labelled prices
    /// as entered; the labels are reordered with it.
    /// </summary>
    private IReadOnlyList<PriceLine<TPriceKind>> Prices(IReadOnlyList<PriceLineDefinition<TPriceKind>> definitions, Currency? currency, out IReadOnlyList<int> labelOrder)
    {
        labelOrder = [];
        if (definitions.Count > _rules.MaxPrices)
        {
            Add(OfferingFields.Prices, Invariant($"There cannot be more than {_rules.MaxPrices} prices."));
            return [];
        }

        var lines = new List<(int Order, PriceLine<TPriceKind> Line, int? Label)>();
        var seen = new HashSet<TPriceKind>();
        var labelled = 0;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A price is null.");
            var rule = _rules.PriceKindOf(definition.Kind, out var order);
            if (rule is null)
            {
                Add(OfferingFields.PriceKind(index), "The price kind is not supported.");
            }
            else if (!rule.Repeatable && !seen.Add(definition.Kind))
            {
                Add(OfferingFields.PriceKind(index), "This price kind can appear only once.");
            }

            int? label = rule?.NeedsLabel == true ? labelled++ : null;
            if (Amount(definition.Amount, currency, OfferingFields.PriceAmount(index), "Price") is { } amount && rule is not null)
            {
                lines.Add((order, new PriceLine<TPriceKind>(definition.Kind, amount), label));
            }
        }

        var sorted = lines.OrderBy(line => line.Order).ToList();
        labelOrder = [.. sorted.Where(line => line.Label.HasValue).Select(line => line.Label!.Value)];
        return [.. sorted.Select(line => line.Line)];
    }

    private Money? Amount(decimal value, Currency? currency, string field, string label)
    {
        if (currency is null)
        {
            return null;
        }

        var money = Money.CreateFromInput(value, currency, field, label);
        if (money.IsFailure)
        {
            _errors.Add(money);
            return null;
        }

        if (!money.Value.IsPositive)
        {
            Add(field, $"{label} must be greater than zero.");
            return null;
        }

        if (value > _rules.MaxPrice)
        {
            Add(field, Invariant($"{label} cannot exceed {_rules.MaxPrice}."));
            return null;
        }

        return money.Value;
    }

    private ParticipantRange? Participants(ParticipantRange? range)
    {
        if (range is null || (range.Min is null && range.Max is null))
        {
            return null;
        }

        var valid = true;
        if (range.Min is { } min && (min < 1 || min > _rules.MaxParticipants))
        {
            valid = Add(OfferingFields.ParticipantsMin, Invariant($"Smallest group must be between 1 and {_rules.MaxParticipants}."));
        }

        if (range.Max is { } max && (max < 1 || max > _rules.MaxParticipants))
        {
            valid = Add(OfferingFields.ParticipantsMax, Invariant($"Largest group must be between 1 and {_rules.MaxParticipants}."));
        }
        else if (valid && range.Min is { } smallest && range.Max is { } largest && largest < smallest)
        {
            valid = Add(OfferingFields.ParticipantsMax, "The largest group cannot be smaller than the smallest.");
        }

        return valid ? range : null;
    }

    private GroupCondition? GroupConditionOf(GroupConditionDefinition definition, ParticipantRange? participants, Currency? currency)
    {
        var valid = true;
        if (definition.MinimumParticipants is not { } minimum)
        {
            valid = Add(OfferingFields.GroupConditionMinimumParticipants, GroupConditionTogether);
            minimum = 0;
        }
        else if (minimum < 1 || minimum > Math.Min(participants?.Max ?? _rules.MaxParticipants, _rules.MaxParticipants))
        {
            valid = Add(OfferingFields.GroupConditionMinimumParticipants, "Minimum participants must be between 1 and the largest group size.");
        }

        Money? surcharge = null;
        if (definition.SurchargePerMissing is not { } amount)
        {
            valid = Add(OfferingFields.GroupConditionSurchargePerMissing, GroupConditionTogether);
        }
        else
        {
            surcharge = Amount(amount, currency, OfferingFields.GroupConditionSurchargePerMissing, "Surcharge");
            valid &= surcharge is not null;
        }

        if (definition.DueDays is not { } dueDays)
        {
            valid = Add(OfferingFields.GroupConditionDueDays, GroupConditionTogether);
            dueDays = 0;
        }
        else if (dueDays is < 1 or > MaxDueDays)
        {
            valid = Add(OfferingFields.GroupConditionDueDays, "Due days must be between 1 and 365.");
        }

        return valid ? new GroupCondition(minimum, surcharge!, dueDays) : null;
    }

    private PaymentTerms? PaymentTermsOf(PaymentTerms? terms)
    {
        if (terms is null || (terms.DepositPercent is null && terms.Instalments is null && terms.BalanceDueDays is null))
        {
            return null;
        }

        var valid = true;
        if (terms.DepositPercent is < 1 or > 100)
        {
            valid = Add(OfferingFields.PaymentTermsDepositPercent, "Deposit must be between 1 and 100 percent.");
        }

        if (terms.Instalments is < 1 or > 24)
        {
            valid = Add(OfferingFields.PaymentTermsInstalments, "Instalments must be between 1 and 24.");
        }

        if (terms.BalanceDueDays is < 1 or > MaxDueDays)
        {
            valid = Add(OfferingFields.PaymentTermsBalanceDueDays, "Balance due days must be between 1 and 365.");
        }

        return valid ? terms : null;
    }

    private IReadOnlyList<Metric<TMetric>> Metrics(IReadOnlyList<Metric<TMetric>> definitions, MetricScope scope, string listField)
    {
        var metrics = new List<(int Order, Metric<TMetric> Metric)>();
        var seen = new HashSet<TMetric>();
        foreach (var metric in definitions)
        {
            ArgumentNullException.ThrowIfNull(metric, nameof(definitions));
            var rule = _rules.MetricOf(metric.Kind, out var order);
            if (rule is null)
            {
                Add(listField, "A metric kind is not supported.");
                continue;
            }

            var field = listField + "." + EnumNames.CamelCase(metric.Kind);
            if ((rule.Scope & scope) == 0)
            {
                Add(field, "This metric is not allowed here.");
            }
            else if (!seen.Add(metric.Kind))
            {
                Add(field, "This metric can appear only once.");
            }
            else if (MetricValid(metric, rule, field))
            {
                metrics.Add((order, metric));
            }
        }

        return [.. metrics.OrderBy(metric => metric.Order).Select(metric => metric.Metric)];
    }

    private bool MetricValid(Metric<TMetric> metric, MetricRule<TMetric> rule, string field)
    {
        var valid = MetricValue(metric.Value, rule, field, "The value");
        if (metric.Upper is { } upper)
        {
            var upperField = field + ".upper";
            if (!rule.AllowsRange)
            {
                valid = Add(upperField, "This metric does not allow a range.");
            }
            else if (upper <= metric.Value)
            {
                valid = Add(upperField, "The upper value must be greater than the value.");
            }
            else
            {
                valid &= MetricValue(upper, rule, upperField, "The upper value");
            }
        }

        if (metric.Approximate && !rule.AllowsApproximate)
        {
            valid = Add(field, "This metric cannot be approximate.");
        }

        return valid;
    }

    private bool MetricValue(decimal value, MetricRule<TMetric> rule, string field, string label)
    {
        if (value < rule.Min || value > rule.Max)
        {
            return Add(field, Invariant($"{label} must be between {rule.Min} and {rule.Max}."));
        }

        return decimal.Round(value, rule.Decimals) == value
            || Add(field, Invariant($"{label} can have at most {rule.Decimals} decimal places."));
    }

    private ReadOnlyCollection<ProgramStep<TImageId, TMetric>> Steps(IReadOnlyList<ProgramStepDefinition<TImageId, TMetric>> definitions, int? durationDays)
    {
        if (definitions.Count > _rules.MaxSteps)
        {
            Add(OfferingFields.Steps, Invariant($"There cannot be more than {_rules.MaxSteps} steps."));
            return [];
        }

        if (_rules.StepCount == OfferingStepCount.MatchesDuration && definitions.Count > 0 && durationDays is { } days && days >= 1
            && days <= _rules.MaxDurationDays && definitions.Count != days)
        {
            Add(OfferingFields.Steps, "The number of steps must equal the duration in days.");
        }

        var steps = new List<ProgramStep<TImageId, TMetric>>(definitions.Count);
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index] ?? throw new ArgumentNullException(nameof(definitions), "A step is null.");
            if (definition.ImageId is { } image && !_images.Exists(image))
            {
                Add(OfferingFields.StepImage(index), "The image does not exist.");
            }

            var metrics = Metrics(definition.Metrics ?? [], MetricScope.Step, OfferingFields.StepMetrics(index));
            steps.Add(new ProgramStep<TImageId, TMetric>(definition.ImageId, metrics));
        }

        return steps.AsReadOnly();
    }

    private IReadOnlyList<TImageId> Images(TImageId? coverId, IReadOnlyList<TImageId> galleryIds)
    {
        if (coverId is { } cover && !_images.Exists(cover))
        {
            Add(OfferingFields.CoverId, "The cover image does not exist.");
        }

        if (galleryIds.Count > _rules.MaxGalleryImages)
        {
            Add(OfferingFields.GalleryIds, Invariant($"A gallery cannot hold more than {_rules.MaxGalleryImages} images."));
        }
        else if (galleryIds.Any(image => !_images.Exists(image)))
        {
            Add(OfferingFields.GalleryIds, "A gallery image does not exist.");
        }
        else if (galleryIds.Distinct().Count() != galleryIds.Count)
        {
            Add(OfferingFields.GalleryIds, "A gallery image appears twice.");
        }

        return [.. galleryIds];
    }

    private static IReadOnlyList<TImageId> AllImages(TImageId? coverId, IReadOnlyList<TImageId> galleryIds, IReadOnlyList<ProgramStep<TImageId, TMetric>> steps) =>
        [.. (coverId is { } cover ? galleryIds.Prepend(cover) : galleryIds)
            .Concat(steps.Where(step => step.ImageId.HasValue).Select(step => step.ImageId!.Value))
            .Distinct()];

    private ReadOnlyCollection<OfferingTranslation> Translations(
        IReadOnlyList<OfferingTranslationDefinition> definitions,
        bool dated,
        IReadOnlyList<int> labelOrder,
        int stepCount,
        IReadOnlyList<TImageId> imageIds)
    {
        var translations = new TranslationListRules(_rules.Cultures, OfferingFields.Translations, "offering").Validate(
            _errors,
            definitions,
            static definition => definition.Culture,
            (culture, definition) => new OfferingTranslationValidator<TImageId, TPriceKind, TMetric>(_errors, _rules, _images, culture)
                .Validate(definition, dated, labelOrder, stepCount, imageIds),
            nameof(definitions));

        return new ReadOnlyCollection<OfferingTranslation>([.. translations]);
    }

    private bool InYears(int year) => year >= _rules.MinYear && year <= _rules.MaxYear;

    private bool Add(string field, string message)
    {
        _errors.Add(Result.Invalid(detail: message, field: field, message: message));
        return false;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
