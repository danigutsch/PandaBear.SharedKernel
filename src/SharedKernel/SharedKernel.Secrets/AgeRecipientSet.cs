using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Secrets;

/// <summary>
/// A non-empty set of distinct <see cref="AgeRecipient"/> values. Equality ignores order, so a reordered set is equal and a
/// subset or superset is not.
/// </summary>
public sealed class AgeRecipientSet : ValueObject
{
    private AgeRecipientSet(AgeRecipient[] recipients) => Recipients = recipients;

    /// <summary>Gets the recipients, ordered by text.</summary>
    public IReadOnlyList<AgeRecipient> Recipients
    {
        get;
    }

    /// <summary>Gets the number of recipients.</summary>
    public int Count => Recipients.Count;

    /// <summary>Creates a set. The input must be non-empty and free of duplicates and nulls.</summary>
    /// <param name="recipients">The recipients.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The set or a validation error that never echoes a recipient.</returns>
    public static Result<AgeRecipientSet> Create(IEnumerable<AgeRecipient?>? recipients, string field = "recipients")
    {
        if (recipients is null)
        {
            return Invalid(field, "Recipients are required.");
        }

        var items = recipients.ToArray();
        if (items.Length == 0)
        {
            return Invalid(field, "At least one recipient is required.");
        }

        if (items.Any(static item => item is null))
        {
            return Invalid(field, "A recipient is missing.");
        }

        var sorted = items.OfType<AgeRecipient>().OrderBy(static item => item.Value, StringComparer.Ordinal).ToArray();
        return sorted.DistinctBy(static item => item.Value, StringComparer.Ordinal).Count() != sorted.Length
            ? Invalid(field, "A recipient is listed more than once.")
            : Result.Ok(new AgeRecipientSet(sorted));
    }

    /// <summary>Returns whether the set contains <paramref name="recipient"/>.</summary>
    /// <param name="recipient">The recipient to find.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> is <see langword="null"/>.</exception>
    public bool Contains(AgeRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return Recipients.Any(item => item.Equals(recipient));
    }

    /// <summary>Returns the recipients of this set that <paramref name="other"/> does not contain, ordered by text.</summary>
    /// <param name="other">The recipients to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<AgeRecipient> Except(AgeRecipientSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return [.. Recipients.Where(item => !other.Contains(item))];
    }

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", Recipients);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() => Recipients.Select(static item => item.Value);

    private static Result<AgeRecipientSet> Invalid(string field, string message) => Result.Invalid<AgeRecipientSet>(message, field, message);
}
