namespace SharedKernel.Results;

/// <summary>
/// Represents error details for a failed result.
/// </summary>
/// <param name="Detail">Specific explanation of the problem instance.</param>
/// <param name="ValidationErrors">Optional validation errors keyed by field name.</param>
public sealed record ResultError(
    string Detail,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
