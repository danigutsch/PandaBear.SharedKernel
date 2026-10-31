using PandaBear.HttpClients;

namespace PandaBear.Blazor;

/// <summary>
/// Maps <see cref="HttpOutcomeKind" /> values to fixed, caller-supplied user copy.
/// </summary>
/// <remarks>
/// The catalog never reads <see cref="HttpOutcome.Message" /> or <see cref="HttpOutcome.ValidationErrors" />:
/// server-authored text cannot reach a free-form message. Show field-keyed validation errors through
/// <see cref="ServerValidationBinder" /> instead. Instances are immutable.
/// </remarks>
public sealed class HttpOutcomeMessages
{
    private readonly Dictionary<HttpOutcomeKind, string> _messages;

    /// <summary>Initializes a catalog whose copy is used for every kind without its own entry.</summary>
    /// <param name="fallback">The nonblank copy for unconfigured kinds.</param>
    /// <exception cref="ArgumentException">The copy is null or blank.</exception>
    public HttpOutcomeMessages(string fallback)
        : this(fallback, [])
    {
    }

    private HttpOutcomeMessages(string fallback, Dictionary<HttpOutcomeKind, string> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        Fallback = fallback;
        _messages = messages;
    }

    /// <summary>Gets the copy used for kinds without their own entry.</summary>
    public string Fallback
    {
        get;
    }

    /// <summary>Returns a new catalog that uses <paramref name="message" /> for <paramref name="kind" />.</summary>
    /// <param name="kind">The outcome kind.</param>
    /// <param name="message">The nonblank copy.</param>
    /// <returns>A new catalog; this instance is unchanged.</returns>
    /// <exception cref="ArgumentException">The copy is null or blank.</exception>
    public HttpOutcomeMessages With(HttpOutcomeKind kind, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new HttpOutcomeMessages(Fallback, new Dictionary<HttpOutcomeKind, string>(_messages) { [kind] = message });
    }

    /// <summary>Returns the copy for an outcome kind.</summary>
    /// <param name="kind">The outcome kind.</param>
    /// <returns>The configured copy, or <see cref="Fallback" />.</returns>
    public string For(HttpOutcomeKind kind) => _messages.TryGetValue(kind, out var message) ? message : Fallback;

    /// <summary>Returns the copy for an outcome, ignoring its server-provided message and validation text.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The configured copy, or <see cref="Fallback" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outcome"/> is <see langword="null"/>.</exception>
    public string For(HttpOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return For(outcome.Kind);
    }
}
