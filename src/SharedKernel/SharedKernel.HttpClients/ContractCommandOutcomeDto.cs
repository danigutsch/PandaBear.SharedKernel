using System.Net;

namespace SharedKernel.HttpClients;

/// <summary>
/// Represents the contract-level outcome of an HTTP command request.
/// </summary>
public sealed record ContractCommandOutcomeDto
{
    /// <summary>
    /// Gets the outcome kind.
    /// </summary>
    public required ContractCommandOutcomeKind Kind { get; init; }

    /// <summary>
    /// Gets the HTTP status code returned by the API.
    /// </summary>
    public required HttpStatusCode StatusCode { get; init; }

    /// <summary>
    /// Gets the created resource location when supplied by the API.
    /// </summary>
    public Uri? Location { get; init; }

    /// <summary>
    /// Gets validation errors when the API returned a validation problem.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>
    /// Gets a diagnostic message suitable for logs or tests, not direct UI display.
    /// </summary>
    public string? Message { get; init; }
}
