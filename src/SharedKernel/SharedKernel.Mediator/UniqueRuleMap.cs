namespace PandaBear.Mediator;

/// <summary>
/// Maps the application rule names carried by <see cref="BuildingBlocks.UniqueConstraintViolationException"/> to the field and
/// the caller-facing message of a validation failure. Configure it once at startup through
/// <see cref="UniqueViolationServiceCollectionExtensions.AddUniqueViolationHandling(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{UniqueRuleMap})"/>.
/// </summary>
/// <remarks>
/// The map is meant to be filled during startup and only read afterwards; it is not synchronized for concurrent writes.
/// Rule names are compared ordinally.
/// </remarks>
public sealed class UniqueRuleMap
{
    private readonly Dictionary<string, UniqueRuleError> rules = new(StringComparer.Ordinal);

    /// <summary>Maps a rule to a field error.</summary>
    /// <param name="rule">The application-defined rule name, as thrown in the exception.</param>
    /// <param name="field">The field the validation failure names.</param>
    /// <param name="message">The fixed, caller-authored message for the field.</param>
    /// <returns>This map, for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="rule"/>, <paramref name="field"/> or <paramref name="message"/> is <see langword="null"/>, empty or white space,
    /// or when <paramref name="rule"/> is already mapped.
    /// </exception>
    public UniqueRuleMap Add(string rule, string field, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!rules.TryAdd(rule, new UniqueRuleError(field, message)))
        {
            throw new ArgumentException("The unique rule is already mapped.", nameof(rule));
        }

        return this;
    }

    /// <summary>Looks up the error of a rule.</summary>
    /// <param name="rule">The rule name.</param>
    /// <param name="error">The mapped error when found.</param>
    /// <returns><see langword="true"/> when the rule is mapped.</returns>
    internal bool TryGet(string rule, out UniqueRuleError error) => rules.TryGetValue(rule, out error);
}
