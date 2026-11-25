using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Offerings;

/// <summary>Creates and restores <see cref="Offering{TImageId, TPriceKind, TMetric}"/> values.</summary>
public static class Offering
{
    /// <summary>Creates an offering after validating every field and the publish rule of each published translation.</summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The application's offering rules.</param>
    /// <param name="images">The application's image knowledge.</param>
    /// <returns>
    /// The offering, or every validation error with its field path (see <see cref="OfferingFields"/>). To store it, in an event or a
    /// row, store <see cref="Offering{TImageId, TPriceKind, TMetric}.ToDefinition"/> of the result, never the entered definition: the
    /// result is sanitized, its prices and labels are sorted, and its slugs are derived.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument, the schedule, or an entry of a list in the definition is <see langword="null"/>.</exception>
    public static Result<Offering<TImageId, TPriceKind, TMetric>> Create<TImageId, TPriceKind, TMetric>(
        OfferingDefinition<TImageId, TPriceKind, TMetric> definition,
        OfferingRules<TPriceKind, TMetric> rules,
        IImageEligibility<TImageId> images)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        var validated = Validate(definition, rules, images);
        return validated.IsSuccess
            ? Result.Ok(new Offering<TImageId, TPriceKind, TMetric>(validated.Value))
            : validated.ConvertError<OfferingData<TImageId, TPriceKind, TMetric>, Offering<TImageId, TPriceKind, TMetric>>();
    }

    /// <summary>
    /// For persistence mappers and event replay: rebuilds an offering from a stored definition, such as one that
    /// <see cref="Offering{TImageId, TPriceKind, TMetric}.ToDefinition"/> produced or an event carries. It is structural only: it refuses
    /// what later reads cannot handle (a missing value, an undefined availability, price kind, metric kind or status, two translations of
    /// one culture, a malformed culture, currency code or slug, dates that are not a range, a duration below one day, prices or a group
    /// condition without a currency, an incomplete group condition), and runs no lengths, limits or publish rules, so never use it for
    /// input. From the rules it reads only the price-kind flags (<see cref="PriceKindRule{TPriceKind}.IsBase"/> and
    /// <see cref="PriceKindRule{TPriceKind}.NeedsLabel"/>), so the from price and the labelled prices of a replayed offering follow the
    /// current rules; a stored culture the rules no longer list is kept.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="definition">The stored definition.</param>
    /// <param name="rules">The application's offering rules.</param>
    /// <returns>The offering.</returns>
    /// <exception cref="ArgumentNullException">An argument, the schedule, or an entry of a list in the definition is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The definition breaks one of the structural rules above.</exception>
    public static Offering<TImageId, TPriceKind, TMetric> Restore<TImageId, TPriceKind, TMetric>(
        OfferingDefinition<TImageId, TPriceKind, TMetric> definition,
        OfferingRules<TPriceKind, TMetric> rules)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rules);

        return new Offering<TImageId, TPriceKind, TMetric>(OfferingRestoration.Restore(definition, rules));
    }

    /// <summary>Validates a definition against the rules.</summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="images">The image knowledge.</param>
    /// <returns>The data, or every error.</returns>
    /// <exception cref="ArgumentNullException">An argument, the schedule, or an entry of a list in the definition is <see langword="null"/>.</exception>
    internal static Result<OfferingData<TImageId, TPriceKind, TMetric>> Validate<TImageId, TPriceKind, TMetric>(
        OfferingDefinition<TImageId, TPriceKind, TMetric> definition,
        OfferingRules<TPriceKind, TMetric> rules,
        IImageEligibility<TImageId> images)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(images);

        return new OfferingValidator<TImageId, TPriceKind, TMetric>(rules, images).Validate(definition);
    }

    /// <summary>Reads the culture of a translation; used to build <see cref="TranslationSet{T}"/> values.</summary>
    /// <param name="translation">The translation.</param>
    /// <returns>Its culture.</returns>
    internal static ContentCulture CultureOf(OfferingTranslation translation) => translation.Culture;
}
