namespace SharedKernel.HttpClients;

/// <summary>
/// Represents the RFC 7807 validation problem members used by contract-owned API clients.
/// </summary>
public sealed record ContractValidationProblemDto
{
    /// <summary>
    /// Gets the validation errors keyed by field name.
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; init; }
}
