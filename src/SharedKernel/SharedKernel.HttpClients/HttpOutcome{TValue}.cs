using System.Net;

namespace PandaBear.HttpClients;

/// <summary>
/// Represents the outcome of an HTTP request that materializes a response value.
/// </summary>
/// <typeparam name="TValue">The materialized response contract type.</typeparam>
public sealed record HttpOutcome<TValue>
{
    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public required HttpOutcomeKind Kind
    {
        get; init;
    }

    /// <summary>
    /// Gets the HTTP status code retained by the outcome, or <see langword="null" /> when unavailable.
    /// </summary>
    public HttpStatusCode? StatusCode
    {
        get; init;
    }

    /// <summary>
    /// Gets the materialized value when the request succeeded.
    /// </summary>
    public TValue? Value
    {
        get; init;
    }

    /// <summary>
    /// Gets the response ETag header exactly as received, including any weak-validator prefix, when retained for a successful outcome.
    /// </summary>
    public string? ETag
    {
        get; init;
    }

    /// <summary>
    /// Gets the response Location header when retained for a successful outcome.
    /// </summary>
    public Uri? Location
    {
        get; init;
    }

    /// <summary>
    /// Gets usable field or model validation errors parsed from an HTTP 400 response.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors
    {
        get; init;
    }

    /// <summary>
    /// Gets an optional diagnostic message. Mapper-generated failures use fixed messages; caller-supplied messages must exclude sensitive data.
    /// </summary>
    public string? Message
    {
        get; init;
    }
}
